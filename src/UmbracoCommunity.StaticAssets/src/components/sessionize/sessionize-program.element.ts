import { LitElement, css, html, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import {
  SessionizeService,
  SessionizeSchedule,
  SessionizeSession,
  SessionizeSpeaker,
  SessionizeTimeSlot,
  SessionizeRoom,
} from "../../services/sessionize.service.js";
import { DcDialogHandler } from "../dialog/dialog.handler.js";
import { SessionizeSessionDialogElement } from "./sessionize-session-dialog.element.js";

const elementName = "dc-sessionize-program";

// Source timezone for Sessionize data (CodeGarden is in Denmark)
const SOURCE_TIMEZONE = "Europe/Copenhagen";

// Available timezones for the dropdown
const TIMEZONE_OPTIONS = [
  { value: "Europe/Copenhagen", label: "Copenhagen (CEST)" },
  { value: "Europe/London", label: "London (BST)" },
  { value: "Europe/Paris", label: "Paris (CEST)" },
  { value: "Europe/Berlin", label: "Berlin (CEST)" },
  { value: "America/New_York", label: "New York (EDT)" },
  { value: "America/Chicago", label: "Chicago (CDT)" },
  { value: "America/Denver", label: "Denver (MDT)" },
  { value: "America/Los_Angeles", label: "Los Angeles (PDT)" },
  { value: "Asia/Tokyo", label: "Tokyo (JST)" },
  { value: "Asia/Singapore", label: "Singapore (SGT)" },
  { value: "Australia/Sydney", label: "Sydney (AEST)" },
  { value: "UTC", label: "UTC" },
];

@customElement(elementName)
export class SessionizeProgramElement extends LitElement {
  @state()
  private _schedule: SessionizeSchedule[] = [];

  @state()
  private _selectedDayIndex = 0;

  @state()
  private _selectedTimezone = SOURCE_TIMEZONE;

  @state()
  private _showServiceSessions = true;

  @state()
  private _loading = true;

  @state()
  private _error: string | null = null;

  #dialogHandler = new DcDialogHandler();

  connectedCallback() {
    super.connectedCallback();
    this.#loadSchedule();
  }

  async #loadSchedule() {
    try {
      this._loading = true;
      this._error = null;

      const schedule = await SessionizeService.getSchedule();
      this._schedule = schedule;

      // Select the default day if specified
      const defaultIndex = schedule.findIndex((day) => day.isDefault);
      if (defaultIndex >= 0) {
        this._selectedDayIndex = defaultIndex;
      }
    } catch (error) {
      this._error =
        error instanceof Error ? error.message : "Failed to load program";
      console.error("Error loading program:", error);
    } finally {
      this._loading = false;
    }
  }

  #formatDate(dateString: string): string {
    try {
      const date = new Date(dateString);
      return date.toLocaleDateString("en-GB", {
        weekday: "long",
        day: "numeric",
        month: "long",
      });
    } catch {
      return dateString;
    }
  }

  #formatTime(timeString: string, dateString?: string): string {
    try {
      let date: Date;

      // Check if timeString is already a full ISO date or just a time
      if (timeString.includes("T") || timeString.includes("-")) {
        // Full ISO datetime string
        date = new Date(timeString);
      } else if (dateString) {
        // Just a time string (e.g., "09:00:00"), combine with the day's date
        date = new Date(`${dateString}T${timeString}`);
      } else {
        // Fallback: try to parse as-is, or return the raw string
        date = new Date(timeString);
      }

      if (isNaN(date.getTime())) {
        // If still invalid, just return the time portion
        return timeString.substring(0, 5); // "09:00:00" -> "09:00"
      }

      return date.toLocaleTimeString("en-GB", {
        hour: "2-digit",
        minute: "2-digit",
        timeZone: this._selectedTimezone,
      });
    } catch {
      return timeString;
    }
  }

  #onTimezoneChange(e: Event) {
    const select = e.target as HTMLSelectElement;
    this._selectedTimezone = select.value;
  }

  #onServiceSessionsToggle(e: Event) {
    const checkbox = e.target as HTMLInputElement;
    this._showServiceSessions = checkbox.checked;
  }

  #renderTimezoneSelector() {
    return html`
      <div class="timezone-selector">
        <label for="timezone-select">Timezone:</label>
        <select
          id="timezone-select"
          @change=${this.#onTimezoneChange}
          .value=${this._selectedTimezone}
        >
          ${TIMEZONE_OPTIONS.map(
            (tz) => html`
              <option value=${tz.value} ?selected=${tz.value === this._selectedTimezone}>
                ${tz.label}
              </option>
            `
          )}
        </select>
      </div>
    `;
  }

  #renderServiceSessionsToggle() {
    return html`
      <label class="service-sessions-toggle">
        <input
          type="checkbox"
          ?checked=${this._showServiceSessions}
          @change=${this.#onServiceSessionsToggle}
        />
        <span class="toggle-label">Show breaks & service sessions</span>
      </label>
    `;
  }

  #openSessionDialog(session: SessionizeSession) {
    const dialog = new SessionizeSessionDialogElement();
    dialog.session = session;
    this.#dialogHandler.open(dialog);
  }

  #selectDay(index: number) {
    this._selectedDayIndex = index;
  }

  #renderLoading() {
    return html`
      <div class="loading">
        <div class="spinner"></div>
        <p>Loading program...</p>
      </div>
    `;
  }

  #renderError() {
    return html`
      <div class="error">
        <p>${this._error}</p>
        <button @click=${this.#loadSchedule}>Try again</button>
      </div>
    `;
  }

  #renderTabs() {
    if (this._schedule.length <= 1) return nothing;

    return html`
      <div class="program-tabs" role="tablist">
        ${this._schedule.map(
          (day, index) => html`
            <button
              role="tab"
              class="program-tab ${index === this._selectedDayIndex ? "active" : ""}"
              aria-selected=${index === this._selectedDayIndex}
              @click=${() => this.#selectDay(index)}
            >
              ${this.#formatDate(day.date)}
            </button>
          `
        )}
      </div>
    `;
  }

  #renderSessionCard(session: SessionizeSession | undefined, room: SessionizeRoom) {
    if (!session) {
      return html`<div class="session-card empty"></div>`;
    }

    // Hide service sessions if toggle is off (but keep empty cell for grid layout)
    if (session.isServiceSession && !this._showServiceSessions) {
      return html`<div class="session-card empty"></div>`;
    }

    const speakerNames = session.speakers?.map((s) => s.fullName).join(", ") || "";

    return html`
      <div
        class="session-card ${session.isServiceSession ? "service-session" : ""}"
        @click=${() => this.#openSessionDialog(session)}
        role="button"
        tabindex="0"
        @keydown=${(e: KeyboardEvent) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            this.#openSessionDialog(session);
          }
        }}
      >
        <h4 class="session-title">${session.title}</h4>
        ${when(
          speakerNames,
          () => html`<p class="session-speakers">${speakerNames}</p>`
        )}
        <p class="session-room">${room.name}</p>
      </div>
    `;
  }

  #renderTimeSlot(timeSlot: SessionizeTimeSlot, rooms: SessionizeRoom[], dayDate: string) {
    return html`
      <div class="time-slot">
        <div class="time-slot-time">
          ${this.#formatTime(timeSlot.slotStart, dayDate)}
        </div>
        <div class="time-slot-sessions">
          ${rooms.map((headerRoom) => {
            const slotRoom = timeSlot.rooms.find((r) => r.id === headerRoom.id);
            return this.#renderSessionCard(slotRoom?.session, headerRoom);
          })}
        </div>
      </div>
    `;
  }

  #renderScheduleGrid() {
    const selectedDay = this._schedule[this._selectedDayIndex];
    if (!selectedDay) {
      return html`<div class="empty-state">No schedule available.</div>`;
    }

    const rooms = selectedDay.rooms;

    return html`
      <div class="schedule-grid">
        <div class="schedule-header">
          <div class="header-time">Time</div>
          <div class="header-rooms">
            ${rooms.map(
              (room) => html`<div class="header-room">${room.name}</div>`
            )}
          </div>
        </div>
        <div class="schedule-body">
          ${selectedDay.timeSlots.map((slot) =>
            this.#renderTimeSlot(slot, rooms, selectedDay.date)
          )}
        </div>
      </div>
    `;
  }

  #renderMobileSchedule() {
    const selectedDay = this._schedule[this._selectedDayIndex];
    if (!selectedDay) {
      return html`<div class="empty-state">No schedule available.</div>`;
    }

    return html`
      <div class="schedule-mobile">
        ${selectedDay.timeSlots.map(
          (slot) => html`
            <div class="mobile-time-slot">
              <div class="mobile-time">${this.#formatTime(slot.slotStart, selectedDay.date)}</div>
              <div class="mobile-sessions">
                ${slot.rooms
                  .filter((room) => room.session && (this._showServiceSessions || !room.session.isServiceSession))
                  .map(
                    (room) => html`
                      <div
                        class="mobile-session-card ${room.session?.isServiceSession ? "service-session" : ""}"
                        @click=${() => room.session && this.#openSessionDialog(room.session)}
                        role="button"
                        tabindex="0"
                        @keydown=${(e: KeyboardEvent) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            room.session && this.#openSessionDialog(room.session);
                          }
                        }}
                      >
                        <span class="mobile-room-badge">${room.name}</span>
                        <h4 class="session-title">${room.session?.title}</h4>
                        ${when(
                          room.session?.speakers?.length,
                          () => html`
                            <p class="session-speakers">
                              ${room.session!.speakers.map((s) => s.fullName).join(", ")}
                            </p>
                          `
                        )}
                      </div>
                    `
                  )}
              </div>
            </div>
          `
        )}
      </div>
    `;
  }

  render() {
    if (this._loading) return this.#renderLoading();
    if (this._error) return this.#renderError();

    if (!this._schedule.length) {
      return html`<div class="empty-state">No program available.</div>`;
    }

    return html`
      <div class="program-container">
        <div class="program-controls">
          ${this.#renderTabs()}
          <div class="program-filters">
            ${this.#renderServiceSessionsToggle()}
            ${this.#renderTimezoneSelector()}
          </div>
        </div>
        <div class="schedule-desktop">${this.#renderScheduleGrid()}</div>
        <div class="schedule-mobile-view">${this.#renderMobileSchedule()}</div>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    .program-container {
      margin-top: var(--unit-md, 2rem);
    }

    /* Controls (tabs + filters) */
    .program-controls {
      display: flex;
      flex-direction: column;
      gap: var(--unit, 1rem);
      margin-bottom: var(--unit-md, 2rem);
    }

    @media (min-width: 768px) {
      .program-controls {
        flex-direction: row;
        justify-content: space-between;
        align-items: center;
      }
    }

    /* Filters container */
    .program-filters {
      display: flex;
      flex-direction: column;
      gap: var(--unit-sm, 0.75rem);
      align-items: flex-start;
    }

    @media (min-width: 768px) {
      .program-filters {
        flex-direction: row;
        align-items: center;
        gap: var(--unit-md, 1.5rem);
      }
    }

    /* Service sessions toggle */
    .service-sessions-toggle {
      display: flex;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
      cursor: pointer;
      user-select: none;
    }

    .service-sessions-toggle input[type="checkbox"] {
      width: 18px;
      height: 18px;
      accent-color: var(--color-blue, #3544b1);
      cursor: pointer;
    }

    .service-sessions-toggle .toggle-label {
      font-size: 0.9rem;
      color: var(--color-dark, #1b264f);
      white-space: nowrap;
    }

    /* Timezone selector */
    .timezone-selector {
      display: flex;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
      flex-shrink: 0;
    }

    .timezone-selector label {
      font-size: 0.9rem;
      font-weight: 500;
      color: var(--color-dark, #1b264f);
      white-space: nowrap;
    }

    .timezone-selector select {
      padding: var(--unit-xs, 0.5rem) var(--unit-sm, 0.75rem);
      font-size: 0.9rem;
      border: 1px solid var(--color-grey, #d1d5db);
      border-radius: var(--border-radius, 6px);
      background: var(--color-white, #fff);
      color: var(--color-dark, #1b264f);
      cursor: pointer;
      min-width: 180px;
      transition: border-color 0.2s ease, box-shadow 0.2s ease;
    }

    .timezone-selector select:hover {
      border-color: var(--color-blue, #3544b1);
    }

    .timezone-selector select:focus {
      outline: none;
      border-color: var(--color-blue, #3544b1);
      box-shadow: 0 0 0 3px rgba(53, 68, 177, 0.15);
    }

    /* Tabs */
    .program-tabs {
      display: flex;
      gap: var(--unit-xs, 0.5rem);
      overflow-x: auto;
      padding-bottom: var(--unit-xs, 0.5rem);
      -webkit-overflow-scrolling: touch;
      flex: 1;
    }

    .program-tab {
      padding: var(--unit-sm, 0.75rem) var(--unit-md, 1.5rem);
      background: var(--color-grey-light, #f5f5f5);
      border: none;
      border-radius: var(--border-radius-lg, 8px);
      cursor: pointer;
      font-size: 1rem;
      font-weight: 500;
      white-space: nowrap;
      transition: background-color 0.2s ease, color 0.2s ease;
      color: var(--color-dark, #1b264f);
    }

    .program-tab:hover {
      background: var(--color-grey, #e5e7eb);
    }

    .program-tab.active {
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
    }

    /* Desktop Grid */
    .schedule-desktop {
      display: none;
    }

    @media (min-width: 1024px) {
      .schedule-desktop {
        display: block;
      }

      .schedule-mobile-view {
        display: none;
      }
    }

    .schedule-grid {
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-radius: var(--border-radius-lg, 8px);
      overflow: hidden;
    }

    .schedule-header {
      display: flex;
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
    }

    .header-time {
      width: 100px;
      flex-shrink: 0;
      padding: var(--unit, 1rem);
      font-weight: 600;
      display: flex;
      align-items: center;
      justify-content: center;
      border-right: 1px solid rgba(255, 255, 255, 0.2);
    }

    .header-rooms {
      display: flex;
      flex: 1;
    }

    .header-room {
      flex: 1;
      padding: var(--unit, 1rem);
      font-weight: 600;
      text-align: center;
      border-right: 1px solid rgba(255, 255, 255, 0.2);
    }

    .header-room:last-child {
      border-right: none;
    }

    .schedule-body {
      background: var(--color-white, #fff);
    }

    .time-slot {
      display: flex;
      border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .time-slot:last-child {
      border-bottom: none;
    }

    .time-slot-time {
      width: 100px;
      flex-shrink: 0;
      padding: var(--unit, 1rem);
      font-weight: 600;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--color-grey-light, #f9f9f9);
      border-right: 1px solid var(--color-grey-light, #e5e7eb);
      color: var(--color-dark, #1b264f);
    }

    .time-slot-sessions {
      display: flex;
      flex: 1;
    }

    .session-card {
      flex: 1;
      padding: var(--unit, 1rem);
      border-right: 1px solid var(--color-grey-light, #e5e7eb);
      cursor: pointer;
      transition: background-color 0.2s ease;
      min-height: 80px;
    }

    .session-card:last-child {
      border-right: none;
    }

    .session-card:hover {
      background: var(--color-grey-light, #f5f5f5);
    }

    .session-card.empty {
      cursor: default;
    }

    .session-card.empty:hover {
      background: transparent;
    }

    .session-card.service-session {
      background: var(--color-grey-light, #f9f9f9);
      cursor: default;
    }

    .session-card.service-session:hover {
      background: var(--color-grey-light, #f9f9f9);
    }

    .session-title {
      margin: 0 0 var(--unit-xs, 0.5rem);
      font-size: 0.95rem;
      font-weight: 600;
      color: var(--color-dark, #1b264f);
      line-height: 1.3;
    }

    .session-speakers {
      margin: 0 0 var(--unit-xs, 0.5rem);
      font-size: 0.85rem;
      color: var(--color-blue, #3544b1);
    }

    .session-room {
      margin: 0;
      font-size: 0.8rem;
      color: var(--color-grey, #6b7280);
      display: none;
    }

    /* Mobile View */
    .schedule-mobile-view {
      display: block;
    }

    @media (min-width: 1024px) {
      .schedule-mobile-view {
        display: none;
      }
    }

    .schedule-mobile {
      display: flex;
      flex-direction: column;
      gap: var(--unit-md, 2rem);
    }

    .mobile-time-slot {
      border-left: 3px solid var(--color-blue, #3544b1);
      padding-left: var(--unit, 1rem);
    }

    .mobile-time {
      font-size: 1.1rem;
      font-weight: 700;
      color: var(--color-blue, #3544b1);
      margin-bottom: var(--unit-sm, 0.75rem);
    }

    .mobile-sessions {
      display: flex;
      flex-direction: column;
      gap: var(--unit-sm, 0.75rem);
    }

    .mobile-session-card {
      background: var(--color-white, #fff);
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-radius: var(--border-radius, 6px);
      padding: var(--unit, 1rem);
      cursor: pointer;
      transition: background-color 0.2s ease, box-shadow 0.2s ease;
    }

    .mobile-session-card:hover {
      background: var(--color-grey-light, #f5f5f5);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    .mobile-session-card.service-session {
      background: var(--color-grey-light, #f9f9f9);
      cursor: default;
    }

    .mobile-session-card.service-session:hover {
      box-shadow: none;
    }

    .mobile-room-badge {
      display: inline-block;
      padding: 0.2rem 0.5rem;
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
      border-radius: var(--border-radius, 4px);
      font-size: 0.75rem;
      font-weight: 600;
      margin-bottom: var(--unit-xs, 0.5rem);
    }

    .mobile-session-card .session-title {
      font-size: 1rem;
    }

    .mobile-session-card .session-speakers {
      font-size: 0.9rem;
    }

    /* Loading & Error */
    .loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-grey, #6b7280);
    }

    .spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-grey-light, #e5e7eb);
      border-top-color: var(--color-blue, #3544b1);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    .loading p {
      margin-top: var(--unit, 1rem);
    }

    .error {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-red, #dc2626);
      text-align: center;
    }

    .error button {
      margin-top: var(--unit, 1rem);
      padding: var(--unit-xs, 0.5rem) var(--unit, 1rem);
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
      border: none;
      border-radius: var(--border-radius, 6px);
      cursor: pointer;
      font-size: 0.875rem;
      transition: background-color 0.2s ease;
    }

    .error button:hover {
      background: var(--color-blue-dark, #2a3690);
    }

    .empty-state {
      text-align: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-grey, #6b7280);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: SessionizeProgramElement;
  }
}
