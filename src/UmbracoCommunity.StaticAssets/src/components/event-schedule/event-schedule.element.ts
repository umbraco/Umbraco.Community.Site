import { LitElement, css, html, nothing, type PropertyValues } from "lit";
import { customElement, property, state } from "lit/decorators.js";

// ── Types (mirrored from Extensions/Client/src/property-editors/event-schedule/types.ts) ──

interface EventScheduleVenue {
  alias: string;
  name: string;
  color: string;
}

interface EventScheduleDay {
  date: string; // ISO date string YYYY-MM-DD
  label: string; // Display label e.g. "08 Jun"
}

interface EventScheduleEvent {
  id: string;
  title: string;
  subtitle: string;
  dayIndex: number;
  startTime: string; // HH:mm
  endTime: string; // HH:mm
  venueAlias: string;
  notIncludedInTicket: boolean;
  columnIndex?: number;
}

interface EventScheduleSettings {
  startHour: number;
  endHour: number;
  granularityMinutes: number;
}

interface EventScheduleValue {
  settings: EventScheduleSettings;
  days: EventScheduleDay[];
  events: EventScheduleEvent[];
  venues: EventScheduleVenue[];
}

// ── Helpers ──

/**
 * Parses a hex color string (e.g. "#ff8800" or "#f80") into [r, g, b].
 */
function parseHexColor(hex: string): [number, number, number] {
  let cleaned = hex.replace("#", "");
  if (cleaned.length === 3) {
    cleaned = cleaned
      .split("")
      .map((c) => c + c)
      .join("");
  }
  const num = parseInt(cleaned, 16);
  return [(num >> 16) & 255, (num >> 8) & 255, num & 255];
}

/**
 * Returns true if the given hex color is perceived as light,
 * meaning dark text should be used on top of it.
 */
function isLightColor(hex: string): boolean {
  const [r, g, b] = parseHexColor(hex);
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.5;
}

/**
 * Converts a time string "HH:mm" to total minutes since midnight.
 */
function timeToMinutes(time: string): number {
  const [h, m] = time.split(":").map(Number);
  return h * 60 + m;
}

/**
 * Formats a time string "HH:mm" (24h) as 12-hour with AM/PM.
 * Returns e.g. "9:00 AM", "1:30 PM".
 */
function formatTime12h(time: string): string {
  const [h, m] = time.split(":").map(Number);
  const period = h < 12 ? "AM" : "PM";
  const display = h === 0 ? 12 : h > 12 ? h - 12 : h;
  return `${display}:${m.toString().padStart(2, "0")} ${period}`;
}

/**
 * Formats an hour number (0-24) as 12-hour AM/PM for the time gutter.
 */
function formatHourLabel(hour: number): { num: string; period: string } {
  const h = hour % 24;
  const period = h < 12 ? "AM" : "PM";
  const display = h === 0 ? 12 : h > 12 ? h - 12 : h;
  return { num: display.toString(), period };
}

/**
 * Formats a day date string as a readable label.
 * E.g. "2025-06-08" becomes "Sunday, June 8"
 */
function formatDayLabel(dateStr: string): string {
  try {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-GB", {
      weekday: "long",
      day: "numeric",
      month: "long",
    });
  } catch {
    return dateStr;
  }
}

// ── Overlap layout ──

interface LayoutedEvent {
  event: EventScheduleEvent;
  column: number;
  totalColumns: number;
}

function layoutEvents(events: EventScheduleEvent[], endHour: number): LayoutedEvent[] {
  if (events.length === 0) return [];

  const endHourMin = endHour * 60;
  const clampEnd = (e: EventScheduleEvent): number => {
    const end = timeToMinutes(e.endTime);
    return end <= timeToMinutes(e.startTime) ? endHourMin : end;
  };

  // Sort by start time, then by columnIndex so preferred column is tried first
  const sorted = [...events].sort((a, b) => {
    const timeDiff = timeToMinutes(a.startTime) - timeToMinutes(b.startTime);
    if (timeDiff !== 0) return timeDiff;
    return (a.columnIndex ?? 0) - (b.columnIndex ?? 0);
  });

  const columns: { endMinutes: number; events: EventScheduleEvent[] }[] = [];
  const eventColumnMap = new Map<string, number>();

  for (const event of sorted) {
    const startMin = timeToMinutes(event.startTime);
    const preferred = event.columnIndex ?? -1;

    if (preferred >= 0) {
      while (columns.length <= preferred) {
        columns.push({ endMinutes: 0, events: [] });
      }
      if (columns[preferred].endMinutes <= startMin) {
        columns[preferred].endMinutes = clampEnd(event);
        columns[preferred].events.push(event);
        eventColumnMap.set(event.id, preferred);
        continue;
      }
    }

    let placed = false;
    for (let col = 0; col < columns.length; col++) {
      if (columns[col].endMinutes <= startMin) {
        columns[col].endMinutes = clampEnd(event);
        columns[col].events.push(event);
        eventColumnMap.set(event.id, col);
        placed = true;
        break;
      }
    }
    if (!placed) {
      eventColumnMap.set(event.id, columns.length);
      columns.push({
        endMinutes: clampEnd(event),
        events: [event],
      });
    }
  }

  const result: LayoutedEvent[] = [];
  for (const event of sorted) {
    const startMin = timeToMinutes(event.startTime);
    const endMin = clampEnd(event);
    let overlappingColumns = 0;
    for (const col of columns) {
      const hasOverlap = col.events.some((e) => {
        const eStart = timeToMinutes(e.startTime);
        const eEnd = clampEnd(e);
        return eStart < endMin && eEnd > startMin;
      });
      if (hasOverlap) overlappingColumns++;
    }
    result.push({
      event,
      column: eventColumnMap.get(event.id)!,
      totalColumns: overlappingColumns,
    });
  }

  return result;
}

// ── Constants ──

const HOUR_HEIGHT = 45; // px per hour on desktop grid
const EVENT_GAP = 3; // px gap between adjacent events
const TIME_GUTTER_WIDTH = 70; // px

const elementName = "dc-event-schedule";

// ── Component ──

@customElement(elementName)
export class EventScheduleElement extends LitElement {
  /** JSON string of the full EventScheduleValue */
  @property({ type: String })
  data = "";

  @state()
  private _schedule: EventScheduleValue | null = null;

  @state()
  private _selectedDayIndex = 0;

  @state()
  private _isMobile = false;

  #resizeObserver?: ResizeObserver;

  connectedCallback() {
    super.connectedCallback();
    this.#parseData();

    this.#resizeObserver = new ResizeObserver((entries) => {
      for (const entry of entries) {
        this._isMobile = entry.contentRect.width < 768;
      }
    });
    this.#resizeObserver.observe(this);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#resizeObserver?.disconnect();
  }

  protected willUpdate(changed: PropertyValues): void {
    if (changed.has("data")) {
      this.#parseData();
    }
  }

  #parseData(): void {
    if (!this.data) {
      this._schedule = null;
      return;
    }
    try {
      this._schedule = JSON.parse(this.data) as EventScheduleValue;
    } catch {
      console.error("dc-event-schedule: failed to parse data attribute");
      this._schedule = null;
    }
  }

  // ── Accessors ──

  #getVenueColor(venueAlias: string): string {
    const venue = this._schedule?.venues.find((v) => v.alias === venueAlias);
    return venue?.color ?? "#cccccc";
  }

  #getVenueName(venueAlias: string): string {
    const venue = this._schedule?.venues.find((v) => v.alias === venueAlias);
    return venue?.name ?? venueAlias;
  }

  #getTextColor(bgHex: string): string {
    return isLightColor(bgHex) ? "#283a97" : "#ffffff";
  }

  #getHours(): number[] {
    if (!this._schedule) return [];
    const { startHour, endHour } = this._schedule.settings;
    const hours: number[] = [];
    for (let h = startHour; h < endHour; h++) {
      hours.push(h);
    }
    return hours;
  }

  #getEventsForDay(dayIndex: number): EventScheduleEvent[] {
    if (!this._schedule) return [];
    return this._schedule.events.filter((e) => e.dayIndex === dayIndex);
  }

  #hasNotIncludedEvents(): boolean {
    if (!this._schedule) return false;
    return this._schedule.events.some((e) => e.notIncludedInTicket);
  }

  // ── Day selection ──

  #selectDay(index: number) {
    this._selectedDayIndex = index;
  }

  // ── Desktop grid rendering ──

  #renderDesktopGrid() {
    if (!this._schedule) return nothing;

    const hours = this.#getHours();
    const days = this._schedule.days;
    const gridHeight = hours.length * HOUR_HEIGHT;

    return html`
      <div class="grid">
        <div class="time-gutter">
          ${hours.map((hour) => {
            const { num, period } = formatHourLabel(hour);
            return html`
              <div class="time-label" style="height: ${HOUR_HEIGHT}px">
                <span class="time-num">${num}</span>
                <span class="time-period">${period}</span>
              </div>
            `;
          })}
        </div>
        <div class="days-container">
          ${days.map(
            (day, dayIndex) => html`
              <div class="day-column">
                <div class="day-header">${formatDayLabel(day.date)}</div>
                <div class="day-body" style="height: ${gridHeight}px">
                  ${hours.map(
                    () => html`<div class="hour-row" style="height: ${HOUR_HEIGHT}px"></div>`
                  )}
                  ${layoutEvents(this.#getEventsForDay(dayIndex), this._schedule!.settings.endHour).map(
                    (layouted) => this.#renderGridEvent(layouted)
                  )}
                </div>
              </div>
            `
          )}
        </div>
      </div>
    `;
  }

  #renderGridEvent(layouted: LayoutedEvent) {
    if (!this._schedule) return nothing;

    const { event, column, totalColumns } = layouted;
    const scheduleStartMinutes = this._schedule.settings.startHour * 60;
    const scheduleEndMinutes = this._schedule.settings.endHour * 60;
    const startMinutes = timeToMinutes(event.startTime);
    const rawEndMinutes = timeToMinutes(event.endTime);
    const crossesBoundary = rawEndMinutes <= startMinutes;
    const endMinutes = crossesBoundary ? scheduleEndMinutes : rawEndMinutes;
    const topPx = ((startMinutes - scheduleStartMinutes) / 60) * HOUR_HEIGHT + EVENT_GAP;
    const heightPx = Math.max(((endMinutes - startMinutes) / 60) * HOUR_HEIGHT - EVENT_GAP * 2, 0);
    const bgColor = this.#getVenueColor(event.venueAlias);
    const textColor = this.#getTextColor(bgColor);
    const borderRadius = crossesBoundary ? "10px 10px 0 0" : "10px";

    const widthPct = 100 / totalColumns;
    const leftPct = column * widthPct;

    return html`
      <div
        class="grid-event"
        style="
          top: ${topPx}px;
          height: ${heightPx}px;
          left: calc(${leftPct}% + 3px);
          width: calc(${widthPct}% - 6px);
          background-color: ${bgColor};
          color: ${textColor};
          border-radius: ${borderRadius};
        "
        title="${event.title}${event.subtitle ? ` - ${event.subtitle}` : ""}"
      >
        <span class="grid-event-title">${event.title}${event.notIncludedInTicket ? " *" : ""}</span>
        ${event.subtitle
          ? html`<span class="grid-event-subtitle">${event.subtitle}</span>`
          : nothing}
      </div>
    `;
  }

  // ── Mobile rendering ──

  #renderMobileStacked() {
    if (!this._schedule) return nothing;

    return html`
      ${this._schedule.days.map((day, dayIndex) => {
        const events = this.#getEventsForDay(dayIndex);
        const sorted = [...events].sort(
          (a, b) => timeToMinutes(a.startTime) - timeToMinutes(b.startTime)
        );

        return html`
          <div class="mobile-day">
            <h3 class="mobile-day-header">${formatDayLabel(day.date)}</h3>
            ${sorted.length === 0
              ? html`<p class="mobile-day-empty">No events scheduled.</p>`
              : html`
                <div class="mobile-list">
                  ${sorted.map((event) => {
                    const bgColor = this.#getVenueColor(event.venueAlias);
                    const venueName = this.#getVenueName(event.venueAlias);
                    const timeRange = `${formatTime12h(event.startTime)} - ${formatTime12h(event.endTime)}`;

                    return html`
                      <div class="mobile-event" style="border-left-color: ${bgColor}">
                        <span class="mobile-event-time">${timeRange}</span>
                        <span class="mobile-event-title">${event.title}${event.notIncludedInTicket ? " *" : ""}</span>
                        ${event.subtitle
                          ? html`<span class="mobile-event-subtitle">${event.subtitle}</span>`
                          : nothing}
                        <span class="mobile-event-venue">${venueName}</span>
                      </div>
                    `;
                  })}
                </div>
              `}
          </div>
        `;
      })}
    `;
  }

  // ── Legend ──

  #renderLegend() {
    if (!this._schedule || this._schedule.venues.length === 0) return nothing;

    return html`
      <div class="legend">
        <div class="legend-venues">
          ${this._schedule.venues.map(
            (venue) => html`
              <div class="legend-item">
                <span
                  class="legend-swatch"
                  style="background-color: ${venue.color}"
                ></span>
                <span class="legend-label">${venue.name}</span>
              </div>
            `
          )}
        </div>
        ${this.#hasNotIncludedEvents()
          ? html`<p class="legend-note">* not included in the price of the ticket</p>`
          : nothing}
      </div>
    `;
  }

  // ── Main render ──

  render() {
    if (!this._schedule || this._schedule.days.length === 0 || this._schedule.events.length === 0) {
      return nothing;
    }

    if (this._isMobile) {
      return html`
        ${this.#renderMobileStacked()}
        ${this.#renderLegend()}
      `;
    }

    return html`
      ${this.#renderDesktopGrid()}
      ${this.#renderLegend()}
    `;
  }

  // ── Styles ──

  static styles = css`
    :host {
      display: block;
    }

    /* ── Desktop Grid ── */

    .grid {
      display: flex;
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-radius: var(--border-radius, 6px);
      overflow-x: auto;
      background: var(--color-white, #fff);
    }

    .time-gutter {
      flex-shrink: 0;
      width: ${TIME_GUTTER_WIDTH}px;
      border-right: 1px solid var(--color-grey-light, #e5e7eb);
      background: transparent;
    }

    .time-label {
      display: flex;
      align-items: flex-start;
      justify-content: flex-end;
      gap: 2px;
      padding: 4px 8px 0 0;
      box-sizing: border-box;
      transform: translateY(2px);
    }

    .time-num {
      font-size: 0.95rem;
      font-weight: 600;
      line-height: 1;
      color: var(--color-identity-dark, #1b264f);
    }

    .time-period {
      font-size: 0.6rem;
      font-weight: 500;
      line-height: 1;
      color: var(--color-identity-dark, #1b264f);
      margin-top: 1px;
    }

    .days-container {
      display: flex;
      flex: 1;
      min-width: 0;
    }

    .day-column {
      flex: 1;
      min-width: 140px;
      border-right: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .day-column:last-child {
      border-right: none;
    }

    .day-header {
      position: sticky;
      top: 0;
      z-index: 5;
      height: 40px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 0.9rem;
      color: var(--color-identity-dark, #1b264f);
      background: transparent;
      border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .day-body {
      position: relative;
    }

    .hour-row {
      box-sizing: border-box;
      border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .hour-row:last-child {
      border-bottom: none;
    }

    /* ── Grid Events ── */

    .grid-event {
      position: absolute;
      border-radius: 10px;
      padding: 6px 8px;
      overflow: hidden;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 2px;
      cursor: default;
      transition: box-shadow 0.2s ease;
    }

    .grid-event:hover {
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
      z-index: 2;
    }

    .grid-event-title {
      font-size: 0.85rem;
      font-weight: 600;
      line-height: 1.3;
      overflow: hidden;
    }

    .grid-event-subtitle {
      font-size: 0.75rem;
      font-weight: 400;
      font-style: italic;
      opacity: 0.85;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    /* ── Mobile Stacked Days ── */

    .mobile-day {
      margin-bottom: var(--unit-md, 1.5rem);
    }

    .mobile-day:last-child {
      margin-bottom: 0;
    }

    .mobile-day-header {
      font-size: 1.1rem;
      font-weight: 600;
      color: var(--color-identity-dark, #1b264f);
      margin: 0 0 var(--unit-sm, 0.75rem);
      padding-bottom: var(--unit-xs, 0.5rem);
      border-bottom: 2px solid var(--color-identity-blue, #283a97);
    }

    .mobile-day-empty {
      font-size: 0.9rem;
      color: var(--color-grey-dark, #6b7280);
      font-style: italic;
      margin: 0;
    }

    /* ── Mobile Event List ── */

    .mobile-list {
      display: flex;
      flex-direction: column;
      gap: var(--unit-sm, 0.75rem);
    }

    .mobile-event {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: var(--unit-sm, 0.75rem) var(--unit, 1rem);
      background: var(--color-white, #fff);
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-left: 4px solid;
      border-radius: var(--border-radius, 6px);
    }

    .mobile-event-time {
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--color-grey-dark, #6b7280);
    }

    .mobile-event-title {
      font-size: 1rem;
      font-weight: 600;
      color: var(--color-identity-dark, #1b264f);
      line-height: 1.3;
    }

    .mobile-event-subtitle {
      font-size: 0.85rem;
      color: var(--color-identity-dark, #1b264f);
      opacity: 0.75;
    }

    .mobile-event-venue {
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--color-grey-dark, #6b7280);
    }

    /* ── Legend ── */

    .legend {
      margin-top: var(--unit, 1rem);
      padding-top: var(--unit-sm, 0.75rem);
      border-top: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .legend-venues {
      display: flex;
      flex-wrap: wrap;
      gap: var(--unit-sm, 0.75rem) var(--unit, 1rem);
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .legend-swatch {
      display: inline-block;
      width: 14px;
      height: 14px;
      border-radius: 3px;
      flex-shrink: 0;
    }

    .legend-label {
      font-size: 0.85rem;
      color: var(--color-identity-dark, #1b264f);
    }

    .legend-note {
      margin: var(--unit-xs, 0.5rem) 0 0;
      font-size: 0.8rem;
      font-style: italic;
      color: var(--color-grey-dark, #6b7280);
    }

    /* ── Empty state ── */

    .empty-state {
      text-align: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-grey-dark, #6b7280);
    }
  `;
}

export default EventScheduleElement;

declare global {
  interface HTMLElementTagNameMap {
    "dc-event-schedule": EventScheduleElement;
  }
}
