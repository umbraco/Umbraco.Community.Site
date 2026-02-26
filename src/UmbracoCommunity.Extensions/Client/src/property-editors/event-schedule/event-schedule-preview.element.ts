import {
  LitElement,
  css,
  html,
  customElement,
  property,
  nothing,
} from "@umbraco-cms/backoffice/external/lit";
import type {
  EventScheduleValue,
  EventScheduleVenue,
  EventScheduleEvent,
} from "./types.js";

const HOUR_HEIGHT = 40;
const EVENT_GAP = 2;

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
 * Formats an hour number (0-24) as 12-hour AM/PM (e.g. "06 AM", "01 PM", "12 AM").
 */
function formatHour(hour: number): string {
  const h = hour % 24;
  const period = h < 12 ? "AM" : "PM";
  const display = h === 0 ? 12 : h > 12 ? h - 12 : h;
  return `${display.toString().padStart(2, "0")} ${period}`;
}

interface LayoutedEvent {
  event: EventScheduleEvent;
  column: number;
  totalColumns: number;
}

/**
 * Assigns overlapping events to side-by-side columns within a day.
 * Returns each event with its column index and the total columns in its group.
 */
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

@customElement("event-schedule-preview")
export class EventSchedulePreviewElement extends LitElement {
  @property({ type: Object })
  schedule?: EventScheduleValue;

  @property({ type: Array })
  venues: EventScheduleVenue[] = [];

  @property({ type: Number })
  selectedDayIndex = -1;

  #getVenueColor(venueAlias: string): string {
    const venue = this.venues.find((v) => v.alias === venueAlias);
    return venue?.color ?? "#cccccc";
  }

  #getVenueName(venueAlias: string): string {
    const venue = this.venues.find((v) => v.alias === venueAlias);
    return venue?.name ?? venueAlias;
  }

  #getHours(): number[] {
    if (!this.schedule) return [];
    const { startHour, endHour } = this.schedule.settings;
    const hours: number[] = [];
    for (let h = startHour; h < endHour; h++) {
      hours.push(h);
    }
    return hours;
  }

  #getEventsForDay(dayIndex: number): EventScheduleEvent[] {
    if (!this.schedule) return [];
    return this.schedule.events.filter((e) => e.dayIndex === dayIndex);
  }

  #renderEmpty() {
    return html`
      <div class="empty">
        <p>Add days and events to see the preview</p>
      </div>
    `;
  }

  #renderEvent(layouted: LayoutedEvent) {
    if (!this.schedule) return nothing;

    const { event, column, totalColumns } = layouted;
    const scheduleStartMinutes = this.schedule.settings.startHour * 60;
    const scheduleEndMinutes = this.schedule.settings.endHour * 60;
    const startMinutes = timeToMinutes(event.startTime);
    const rawEndMinutes = timeToMinutes(event.endTime);
    const crossesBoundary = rawEndMinutes <= startMinutes;
    const endMinutes = crossesBoundary ? scheduleEndMinutes : rawEndMinutes;
    const topPx = ((startMinutes - scheduleStartMinutes) / 60) * HOUR_HEIGHT + EVENT_GAP;
    const heightPx = ((endMinutes - startMinutes) / 60) * HOUR_HEIGHT - EVENT_GAP * 2;
    const bgColor = this.#getVenueColor(event.venueAlias);
    const textColor = isLightColor(bgColor) ? "#283a97" : "#ffffff";
    const borderRadius = crossesBoundary ? "8px 8px 0 0" : "8px";

    const widthPct = 100 / totalColumns;
    const leftPct = column * widthPct;

    return html`
      <div
        class="event"
        style="
          top: ${topPx}px;
          height: ${heightPx}px;
          left: calc(${leftPct}% + 2px);
          width: calc(${widthPct}% - 4px);
          background-color: ${bgColor};
          color: ${textColor};
          border-radius: ${borderRadius};
        "
        title="${event.title}${event.subtitle ? ` - ${event.subtitle}` : ""} (${this.#getVenueName(event.venueAlias)})"
      >
        <span class="event-title">${event.title}${event.notIncludedInTicket ? " *" : ""}</span>
        ${event.subtitle
          ? html`<span class="event-subtitle">${event.subtitle}</span>`
          : nothing}
      </div>
    `;
  }

  #renderGrid() {
    if (!this.schedule) return nothing;

    const hours = this.#getHours();
    const allDays = this.schedule.days;
    const gridHeight = hours.length * HOUR_HEIGHT;

    // Show single day or all days
    const hasSingleDay = this.selectedDayIndex >= 0 && this.selectedDayIndex < allDays.length;
    const days = hasSingleDay
      ? [{ day: allDays[this.selectedDayIndex], dayIndex: this.selectedDayIndex }]
      : allDays.map((day, dayIndex) => ({ day, dayIndex }));

    return html`
      <div class="grid">
        <div class="time-gutter">
          ${hours.map(
            (hour) => html`
              <div class="time-label" style="height: ${HOUR_HEIGHT}px">
                ${formatHour(hour)}
              </div>
            `
          )}
        </div>

        ${days.map(
          ({ day, dayIndex }) => html`
            <div class="day-column">
              <div class="day-header">${day.label}</div>
              <div class="day-body" style="height: ${gridHeight}px">
                ${hours.map(
                  () => html`<div class="hour-row" style="height: ${HOUR_HEIGHT}px"></div>`
                )}
                ${layoutEvents(this.#getEventsForDay(dayIndex), this.schedule!.settings.endHour).map(
                  (layouted) => this.#renderEvent(layouted)
                )}
              </div>
            </div>
          `
        )}
      </div>
    `;
  }

  #renderLegend() {
    if (this.venues.length === 0) return nothing;

    return html`
      <div class="legend">
        ${this.venues.map(
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
    `;
  }

  render() {
    const isEmpty =
      !this.schedule ||
      this.schedule.days.length === 0 ||
      this.schedule.events.length === 0;

    if (isEmpty) {
      return this.#renderEmpty();
    }

    return html`
      ${this.#renderGrid()}
      ${this.#renderLegend()}
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
      }

      .empty {
        padding: 24px;
        text-align: center;
        color: var(--uui-color-text-alt, #666);
        font-style: italic;
        border: 1px dashed var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px);
      }

      .grid {
        display: flex;
        border: 1px solid var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px);
        overflow-x: auto;
      }

      .time-gutter {
        flex-shrink: 0;
        width: 60px;
        padding-top: 32px; /* matches day-header height */
        border-right: 1px solid var(--uui-color-border, #e0e0e0);
        background: var(--uui-color-surface, #f9f9f9);
      }

      .time-label {
        display: flex;
        align-items: flex-start;
        justify-content: flex-end;
        padding-right: 8px;
        font-size: 11px;
        color: var(--uui-color-text-alt, #888);
        box-sizing: border-box;
        transform: translateY(-7px);
      }

      .day-column {
        flex: 1;
        min-width: 120px;
        border-right: 1px solid var(--uui-color-border, #e0e0e0);
      }

      .day-column:last-child {
        border-right: none;
      }

      .day-header {
        height: 32px;
        display: flex;
        align-items: center;
        justify-content: center;
        font-weight: 600;
        font-size: 13px;
        border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
        background: var(--uui-color-surface, #f9f9f9);
      }

      .day-body {
        position: relative;
      }

      .hour-row {
        box-sizing: border-box;
        border-bottom: 1px solid var(--uui-color-border-standalone, #eee);
      }

      .hour-row:last-child {
        border-bottom: none;
      }

      .event {
        position: absolute;
        border-radius: 8px;
        padding: 4px 6px;
        overflow: hidden;
        box-sizing: border-box;
        font-size: 11px;
        line-height: 1.3;
        cursor: default;
        display: flex;
        flex-direction: column;
        gap: 1px;
      }

      .event-title {
        font-weight: 600;
        font-size: 12px;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .event-subtitle {
        font-weight: 400;
        font-style: italic;
        opacity: 0.85;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .legend {
        display: flex;
        flex-wrap: wrap;
        gap: 12px;
        padding: 12px 0 0;
      }

      .legend-item {
        display: flex;
        align-items: center;
        gap: 6px;
        font-size: 12px;
      }

      .legend-swatch {
        display: inline-block;
        width: 12px;
        height: 12px;
        border-radius: 2px;
        flex-shrink: 0;
      }

      .legend-label {
        color: var(--uui-color-text, #333);
      }
    `,
  ];
}

export default EventSchedulePreviewElement;

declare global {
  interface HTMLElementTagNameMap {
    "event-schedule-preview": EventSchedulePreviewElement;
  }
}
