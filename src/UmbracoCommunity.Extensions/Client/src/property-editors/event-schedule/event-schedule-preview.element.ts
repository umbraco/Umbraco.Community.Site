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

const HOUR_HEIGHT = 60;

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

@customElement("event-schedule-preview")
export class EventSchedulePreviewElement extends LitElement {
  @property({ type: Object })
  schedule?: EventScheduleValue;

  @property({ type: Array })
  venues: EventScheduleVenue[] = [];

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

  #renderEvent(event: EventScheduleEvent) {
    if (!this.schedule) return nothing;

    const scheduleStartMinutes = this.schedule.settings.startHour * 60;
    const startMinutes = timeToMinutes(event.startTime);
    const endMinutes = timeToMinutes(event.endTime);
    const topPx = ((startMinutes - scheduleStartMinutes) / 60) * HOUR_HEIGHT;
    const heightPx = ((endMinutes - startMinutes) / 60) * HOUR_HEIGHT;
    const bgColor = this.#getVenueColor(event.venueAlias);
    const textColor = isLightColor(bgColor) ? "#1b264f" : "#ffffff";

    return html`
      <div
        class="event"
        style="
          top: ${topPx}px;
          height: ${heightPx}px;
          background-color: ${bgColor};
          color: ${textColor};
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
    const days = this.schedule.days;
    const gridHeight = hours.length * HOUR_HEIGHT;

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
          (day, dayIndex) => html`
            <div class="day-column">
              <div class="day-header">${day.label}</div>
              <div class="day-body" style="height: ${gridHeight}px">
                ${hours.map(
                  () => html`<div class="hour-row" style="height: ${HOUR_HEIGHT}px"></div>`
                )}
                ${this.#getEventsForDay(dayIndex).map((event) =>
                  this.#renderEvent(event)
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
        left: 2px;
        right: 2px;
        border-radius: 3px;
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
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .event-subtitle {
        font-weight: 400;
        opacity: 0.85;
        white-space: nowrap;
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
