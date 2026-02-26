import {
  LitElement,
  css,
  html,
  nothing,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { UmbPropertyEditorConfigCollection } from "@umbraco-cms/backoffice/property-editor";
import type {
  EventScheduleValue,
  EventScheduleVenue,
  EventScheduleEvent,
  EventScheduleDay,
} from "./types.js";
import { DEFAULT_SCHEDULE_VALUE } from "./types.js";

// Side-effect import to register the preview element
import "./event-schedule-preview.element.js";

/**
 * Formats a date string (YYYY-MM-DD) as a short label like "08 Jun".
 */
function formatDateLabel(dateStr: string): string {
  const date = new Date(dateStr + "T00:00:00");
  const day = date.getDate().toString().padStart(2, "0");
  const month = date.toLocaleString("en-US", { month: "short" });
  return `${day} ${month}`;
}

/**
 * Returns tomorrow's date or the day after the last day in the list,
 * formatted as YYYY-MM-DD.
 */
function getNextDate(days: EventScheduleDay[]): string {
  const base =
    days.length > 0 ? new Date(days[days.length - 1].date + "T00:00:00") : new Date();
  base.setDate(base.getDate() + 1);
  return base.toISOString().split("T")[0];
}

@customElement("event-schedule-editor")
export class EventScheduleEditorElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  @property({ type: Object })
  value: EventScheduleValue = { ...DEFAULT_SCHEDULE_VALUE };

  @state()
  private _venues: EventScheduleVenue[] = [];

  @state()
  private _editingEventId: string | null = null;

  @state()
  private _editingDayIndex: number | null = null;

  @state()
  private _activeDayIndex = 0;

  // Form fields for event editing
  @state()
  private _formTitle = "";

  @state()
  private _formSubtitle = "";

  @state()
  private _formStartTime = "09:00";

  @state()
  private _formEndTime = "10:00";

  @state()
  private _formVenueAlias = "";

  @state()
  private _formNotIncludedInTicket = false;

  @state()
  private _formColumnIndex = 0;

  public set config(value: UmbPropertyEditorConfigCollection | undefined) {
    if (!value) return;
    const venuesJson = value.getValueByAlias<string>("venues");
    if (venuesJson) {
      try {
        this._venues = JSON.parse(venuesJson) as EventScheduleVenue[];
      } catch {
        this._venues = [];
      }
    }
    // Set default venue alias for the form
    if (this._venues.length > 0 && !this._formVenueAlias) {
      this._formVenueAlias = this._venues[0].alias;
    }
  }

  #getSchedule(): EventScheduleValue {
    return this.value ?? { ...DEFAULT_SCHEDULE_VALUE };
  }

  #updateValue(partial: Partial<EventScheduleValue>) {
    const current = this.#getSchedule();
    this.value = {
      ...current,
      ...partial,
      venues: this._venues,
    };
    this.dispatchEvent(new UmbChangeEvent());
  }

  // ---------------------------------------------------------------------------
  // Settings
  // ---------------------------------------------------------------------------

  #onStartHourChange(e: Event) {
    const input = e.target as HTMLInputElement;
    const val = Math.max(0, Math.min(23, parseInt(input.value, 10) || 0));
    const schedule = this.#getSchedule();
    this.#updateValue({
      settings: { ...schedule.settings, startHour: val },
    });
  }

  #onEndHourChange(e: Event) {
    const input = e.target as HTMLInputElement;
    const val = Math.max(1, Math.min(24, parseInt(input.value, 10) || 24));
    const schedule = this.#getSchedule();
    this.#updateValue({
      settings: { ...schedule.settings, endHour: val },
    });
  }

  #onGranularityChange(e: Event) {
    const input = e.target as HTMLInputElement;
    const val = Math.max(5, Math.min(60, parseInt(input.value, 10) || 30));
    const schedule = this.#getSchedule();
    this.#updateValue({
      settings: { ...schedule.settings, granularityMinutes: val },
    });
  }

  // ---------------------------------------------------------------------------
  // Day management
  // ---------------------------------------------------------------------------

  #addDay() {
    const schedule = this.#getSchedule();
    const newDate = getNextDate(schedule.days);
    const newDay: EventScheduleDay = {
      date: newDate,
      label: formatDateLabel(newDate),
    };
    this.#updateValue({
      days: [...schedule.days, newDay],
    });
  }

  #changeDayDate(dayIndex: number, e: Event) {
    const input = e.target as HTMLInputElement;
    const newDate = input.value;
    if (!newDate) return;

    const schedule = this.#getSchedule();
    const updatedDays = schedule.days.map((day, i) =>
      i === dayIndex
        ? { ...day, date: newDate, label: formatDateLabel(newDate) }
        : day
    );
    this.#updateValue({ days: updatedDays });
  }

  #removeDay(dayIndex: number) {
    const schedule = this.#getSchedule();
    const updatedDays = schedule.days.filter((_, i) => i !== dayIndex);
    // Remove events for this day and re-index remaining events
    const updatedEvents = schedule.events
      .filter((evt) => evt.dayIndex !== dayIndex)
      .map((evt) => ({
        ...evt,
        dayIndex: evt.dayIndex > dayIndex ? evt.dayIndex - 1 : evt.dayIndex,
      }));
    this.#updateValue({ days: updatedDays, events: updatedEvents });

    // Close form if it was open for this day
    if (this._editingDayIndex === dayIndex) {
      this.#cancelForm();
    }
  }

  // ---------------------------------------------------------------------------
  // Event management
  // ---------------------------------------------------------------------------

  #openAddEventForm(dayIndex: number) {
    this._editingDayIndex = dayIndex;
    this._editingEventId = null;
    this._formTitle = "";
    this._formSubtitle = "";
    this._formStartTime = "09:00";
    this._formEndTime = "10:00";
    this._formVenueAlias =
      this._venues.length > 0 ? this._venues[0].alias : "";
    this._formNotIncludedInTicket = false;
    this._formColumnIndex = 0;
  }

  #openEditEventForm(event: EventScheduleEvent) {
    this._editingDayIndex = event.dayIndex;
    this._editingEventId = event.id;
    this._formTitle = event.title;
    this._formSubtitle = event.subtitle;
    this._formStartTime = event.startTime;
    this._formEndTime = event.endTime;
    this._formVenueAlias = event.venueAlias;
    this._formNotIncludedInTicket = event.notIncludedInTicket;
    this._formColumnIndex = event.columnIndex ?? 0;
  }

  #cancelForm() {
    this._editingDayIndex = null;
    this._editingEventId = null;
  }

  #saveEvent() {
    if (this._editingDayIndex === null) return;
    if (!this._formTitle.trim()) return;

    const schedule = this.#getSchedule();

    const eventData: EventScheduleEvent = {
      id: this._editingEventId ?? `evt-${Date.now()}`,
      title: this._formTitle.trim(),
      subtitle: this._formSubtitle.trim(),
      dayIndex: this._editingDayIndex,
      startTime: this._formStartTime,
      endTime: this._formEndTime,
      venueAlias: this._formVenueAlias,
      notIncludedInTicket: this._formNotIncludedInTicket,
      columnIndex: this._formColumnIndex,
    };

    let updatedEvents: EventScheduleEvent[];
    if (this._editingEventId) {
      // Editing existing event
      updatedEvents = schedule.events.map((evt) =>
        evt.id === this._editingEventId ? eventData : evt
      );
    } else {
      // Adding new event
      updatedEvents = [...schedule.events, eventData];
    }

    this.#updateValue({ events: updatedEvents });
    this.#cancelForm();
  }

  #removeEvent(eventId: string) {
    const schedule = this.#getSchedule();
    const updatedEvents = schedule.events.filter((evt) => evt.id !== eventId);
    this.#updateValue({ events: updatedEvents });

    // Close form if we were editing this event
    if (this._editingEventId === eventId) {
      this.#cancelForm();
    }
  }

  // ---------------------------------------------------------------------------
  // Rendering: Settings
  // ---------------------------------------------------------------------------

  #renderSettings() {
    const schedule = this.#getSchedule();
    const { startHour, endHour, granularityMinutes } = schedule.settings;

    return html`
      <details class="settings-panel">
        <summary>Schedule Settings</summary>
        <div class="settings-grid">
          <label class="setting-field">
            <span class="setting-label">Start hour (0-23)</span>
            <input
              type="number"
              min="0"
              max="23"
              .value=${String(startHour)}
              @change=${this.#onStartHourChange}
            />
          </label>
          <label class="setting-field">
            <span class="setting-label">End hour (1-24)</span>
            <input
              type="number"
              min="1"
              max="24"
              .value=${String(endHour)}
              @change=${this.#onEndHourChange}
            />
          </label>
          <label class="setting-field">
            <span class="setting-label">Granularity (minutes)</span>
            <input
              type="number"
              min="5"
              max="60"
              step="5"
              .value=${String(granularityMinutes)}
              @change=${this.#onGranularityChange}
            />
          </label>
        </div>
      </details>
    `;
  }

  // ---------------------------------------------------------------------------
  // Rendering: Event form
  // ---------------------------------------------------------------------------

  #renderEventForm() {
    return html`
      <div class="event-form">
        <label class="form-field">
          <span class="form-label">Title *</span>
          <input
            type="text"
            .value=${this._formTitle}
            @input=${(e: Event) => {
              this._formTitle = (e.target as HTMLInputElement).value;
            }}
            placeholder="Event title"
          />
        </label>
        <label class="form-field">
          <span class="form-label">Subtitle</span>
          <input
            type="text"
            .value=${this._formSubtitle}
            @input=${(e: Event) => {
              this._formSubtitle = (e.target as HTMLInputElement).value;
            }}
            placeholder="Optional subtitle"
          />
        </label>
        <div class="form-row">
          <label class="form-field">
            <span class="form-label">Start time</span>
            <input
              type="time"
              .value=${this._formStartTime}
              @change=${(e: Event) => {
                this._formStartTime = (e.target as HTMLInputElement).value;
              }}
            />
          </label>
          <label class="form-field">
            <span class="form-label">End time</span>
            <input
              type="time"
              .value=${this._formEndTime}
              @change=${(e: Event) => {
                this._formEndTime = (e.target as HTMLInputElement).value;
              }}
            />
          </label>
        </div>
        ${this._venues.length > 0
          ? html`
              <label class="form-field">
                <span class="form-label">Venue</span>
                <select
                  .value=${this._formVenueAlias}
                  @change=${(e: Event) => {
                    this._formVenueAlias = (e.target as HTMLSelectElement).value;
                  }}
                >
                  ${this._venues.map(
                    (v) => html`
                      <option
                        value=${v.alias}
                        ?selected=${v.alias === this._formVenueAlias}
                      >
                        ${v.name}
                      </option>
                    `
                  )}
                </select>
              </label>
            `
          : nothing}
        <label class="form-field">
          <span class="form-label">Column</span>
          <select
            .value=${String(this._formColumnIndex)}
            @change=${(e: Event) => {
              this._formColumnIndex = parseInt((e.target as HTMLSelectElement).value, 10);
            }}
          >
            <option value="0" ?selected=${this._formColumnIndex === 0}>Left</option>
            <option value="1" ?selected=${this._formColumnIndex === 1}>Right</option>
          </select>
        </label>
        <label class="form-field-inline">
          <input
            type="checkbox"
            .checked=${this._formNotIncludedInTicket}
            @change=${(e: Event) => {
              this._formNotIncludedInTicket = (
                e.target as HTMLInputElement
              ).checked;
            }}
          />
          <span>Not included in ticket</span>
        </label>
        <div class="form-actions">
          <uui-button
            look="primary"
            @click=${this.#saveEvent}
            ?disabled=${!this._formTitle.trim()}
          >
            ${this._editingEventId ? "Update" : "Add"} Event
          </uui-button>
          <uui-button look="secondary" @click=${this.#cancelForm}>
            Cancel
          </uui-button>
        </div>
      </div>
    `;
  }

  // ---------------------------------------------------------------------------
  // Rendering: Day section with events
  // ---------------------------------------------------------------------------

  #getEventsForDay(dayIndex: number): EventScheduleEvent[] {
    const schedule = this.#getSchedule();
    return schedule.events.filter((e) => e.dayIndex === dayIndex);
  }

  #getVenueName(alias: string): string {
    const venue = this._venues.find((v) => v.alias === alias);
    return venue?.name ?? alias;
  }

  #renderEventItem(event: EventScheduleEvent) {
    return html`
      <div class="event-item">
        <div class="event-item-info">
          <span class="event-item-title">${event.title}</span>
          <span class="event-item-detail">
            ${event.startTime} - ${event.endTime}
            &middot; ${this.#getVenueName(event.venueAlias)}
            ${event.notIncludedInTicket ? " (not in ticket)" : ""}
          </span>
        </div>
        <div class="event-item-actions">
          <uui-button
            compact
            look="outline"
            @click=${() => this.#openEditEventForm(event)}
          >
            <uui-icon name="icon-edit"></uui-icon>
          </uui-button>
          <uui-button
            compact
            look="outline"
            @click=${() => this.#removeEvent(event.id)}
          >
            <uui-icon name="icon-trash"></uui-icon>
          </uui-button>
        </div>
      </div>
    `;
  }

  #renderDay(day: EventScheduleDay, dayIndex: number) {
    const events = this.#getEventsForDay(dayIndex);
    const isFormOpenForThisDay = this._editingDayIndex === dayIndex;

    const isOpen = dayIndex === this._activeDayIndex;

    return html`
      <div class="day-section ${isOpen ? "active" : ""}">
        <div class="day-header" @click=${() => { this._activeDayIndex = isOpen ? -1 : dayIndex; }}>
          <uui-icon name=${isOpen ? "icon-navigation-down" : "icon-navigation-right"} class="day-toggle"></uui-icon>
          <span class="day-label">${day.label}</span>
          <span class="day-event-count">${events.length} event${events.length !== 1 ? "s" : ""}</span>
          <input
            type="date"
            .value=${day.date}
            @change=${(e: Event) => this.#changeDayDate(dayIndex, e)}
            @click=${(e: Event) => e.stopPropagation()}
            class="day-date-input"
          />
          <uui-button
            compact
            look="outline"
            @click=${(e: Event) => { e.stopPropagation(); this.#removeDay(dayIndex); }}
          >
            <uui-icon name="icon-trash"></uui-icon>
          </uui-button>
        </div>

        ${isOpen
          ? html`
              <div class="day-body-content">
                ${events.length > 0
                  ? html`
                      <div class="event-list">
                        ${events.map((evt) => html`
                          ${this.#renderEventItem(evt)}
                          ${this._editingEventId === evt.id ? this.#renderEventForm() : nothing}
                        `)}
                      </div>
                    `
                  : nothing}

                ${isFormOpenForThisDay && !this._editingEventId
                  ? this.#renderEventForm()
                  : !isFormOpenForThisDay
                    ? html`
                        <uui-button
                          compact
                          look="outline"
                          @click=${() => this.#openAddEventForm(dayIndex)}
                        >
                          <uui-icon name="icon-add"></uui-icon> Add Event
                        </uui-button>
                      `
                    : nothing}
              </div>
            `
          : nothing}
      </div>
    `;
  }

  // ---------------------------------------------------------------------------
  // Main render
  // ---------------------------------------------------------------------------

  render() {
    const schedule = this.#getSchedule();

    return html`
      <div class="layout">
        <div class="form-panel">
          ${this.#renderSettings()}

          <div class="days-section">
            ${schedule.days.map((day, i) => this.#renderDay(day, i))}

            <uui-button look="primary" @click=${this.#addDay}>
              <uui-icon name="icon-add"></uui-icon> Add Day
            </uui-button>
          </div>
        </div>

        <div class="preview-panel">
          <event-schedule-preview
            .schedule=${schedule}
            .venues=${this._venues}
            .selectedDayIndex=${this._activeDayIndex}
          ></event-schedule-preview>
        </div>
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
      }

      .layout {
        display: flex;
        gap: 24px;
      }

      .form-panel {
        flex: 1;
        min-width: 0;
      }

      .preview-panel {
        flex: 1;
        min-width: 0;
      }

      @media (max-width: 900px) {
        .layout {
          flex-direction: column;
        }
      }

      /* Settings */
      .settings-panel {
        margin-bottom: 16px;
        border: 1px solid var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px);
        padding: 0;
      }

      .settings-panel summary {
        padding: 10px 14px;
        cursor: pointer;
        font-weight: 600;
        font-size: 14px;
        user-select: none;
        background: var(--uui-color-surface, #f9f9f9);
        border-radius: var(--uui-border-radius, 3px);
      }

      .settings-panel[open] summary {
        border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px) var(--uui-border-radius, 3px) 0 0;
      }

      .settings-grid {
        display: flex;
        gap: 16px;
        padding: 14px;
        flex-wrap: wrap;
      }

      .setting-field {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }

      .setting-label {
        font-size: 13px;
        font-weight: 500;
        color: var(--uui-color-text-alt, #666);
      }

      .setting-field input {
        width: 100px;
        padding: 6px 8px;
        border: 1px solid var(--uui-color-border, #ccc);
        border-radius: var(--uui-border-radius, 3px);
        font-size: 14px;
      }

      /* Days */
      .days-section {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      .day-section {
        border: 1px solid var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px);
        padding: 12px;
      }

      .day-header {
        display: flex;
        align-items: center;
        gap: 10px;
        cursor: pointer;
        user-select: none;
      }

      .day-section.active .day-header {
        margin-bottom: 10px;
      }

      .day-toggle {
        font-size: 12px;
        color: var(--uui-color-text-alt, #888);
        flex-shrink: 0;
      }

      .day-label {
        font-weight: 600;
        font-size: 15px;
        min-width: 60px;
      }

      .day-event-count {
        font-size: 12px;
        color: var(--uui-color-text-alt, #888);
        margin-right: auto;
        white-space: nowrap;
      }

      .day-date-input {
        padding: 4px 8px;
        border: 1px solid var(--uui-color-border, #ccc);
        border-radius: var(--uui-border-radius, 3px);
        font-size: 13px;
      }

      /* Event list */
      .event-list {
        display: flex;
        flex-direction: column;
        gap: 6px;
        margin-bottom: 10px;
      }

      .event-item {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 8px 10px;
        background: var(--uui-color-surface, #f9f9f9);
        border: 1px solid var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px);
        gap: 8px;
      }

      .event-item-info {
        display: flex;
        flex-direction: column;
        min-width: 0;
      }

      .event-item-title {
        font-weight: 600;
        font-size: 14px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .event-item-detail {
        font-size: 12px;
        color: var(--uui-color-text-alt, #888);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .event-item-actions {
        display: flex;
        gap: 4px;
        flex-shrink: 0;
      }

      /* Event form */
      .event-form {
        display: flex;
        flex-direction: column;
        gap: 10px;
        padding: 12px;
        margin-top: 8px;
        background: var(--uui-color-surface-alt, #f3f3f3);
        border: 1px solid var(--uui-color-border, #e0e0e0);
        border-radius: var(--uui-border-radius, 3px);
      }

      .form-field {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }

      .form-label {
        font-size: 13px;
        font-weight: 500;
        color: var(--uui-color-text-alt, #666);
      }

      .form-field input,
      .form-field select {
        padding: 6px 8px;
        border: 1px solid var(--uui-color-border, #ccc);
        border-radius: var(--uui-border-radius, 3px);
        font-size: 14px;
      }

      .form-row {
        display: flex;
        gap: 12px;
      }

      .form-row .form-field {
        flex: 1;
      }

      .form-field-inline {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 14px;
        cursor: pointer;
      }

      .form-field-inline input[type="checkbox"] {
        width: 16px;
        height: 16px;
      }

      .form-actions {
        display: flex;
        gap: 8px;
        padding-top: 4px;
      }
    `,
  ];
}

export default EventScheduleEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "event-schedule-editor": EventScheduleEditorElement;
  }
}
