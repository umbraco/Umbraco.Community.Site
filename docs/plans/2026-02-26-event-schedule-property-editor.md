# Event Schedule Property Editor - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a custom Umbraco property editor that lets editors create event schedules via form input with a live visual preview grid, and a frontend Lit component that renders the schedule responsively.

**Architecture:** Single property editor using `Umbraco.Plain.Json` built-in schema (no custom C# needed). Backoffice UI in UmbracoCommunity.Extensions/Client with form + live preview. Frontend display component in UmbracoCommunity.StaticAssets. Venue definitions stored in data type configuration.

**Tech Stack:** Lit 3.x, TypeScript, Umbraco backoffice APIs (`@umbraco-cms/backoffice`), UUI components, PostCSS

---

### Task 1: TypeScript interfaces and shared types

**Files:**
- Create: `src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/types.ts`

**Step 1: Create the shared type definitions**

```typescript
export interface EventScheduleVenue {
  alias: string;
  name: string;
  color: string;
}

export interface EventScheduleDay {
  date: string; // ISO date string YYYY-MM-DD
  label: string; // Display label e.g. "08 Jun"
}

export interface EventScheduleEvent {
  id: string;
  title: string;
  subtitle: string;
  dayIndex: number;
  startTime: string; // HH:mm
  endTime: string; // HH:mm
  venueAlias: string;
  notIncludedInTicket: boolean;
}

export interface EventScheduleSettings {
  startHour: number;
  endHour: number;
  granularityMinutes: number;
}

export interface EventScheduleValue {
  settings: EventScheduleSettings;
  days: EventScheduleDay[];
  events: EventScheduleEvent[];
}

export const DEFAULT_SCHEDULE_VALUE: EventScheduleValue = {
  settings: {
    startHour: 6,
    endHour: 24,
    granularityMinutes: 30,
  },
  days: [],
  events: [],
};
```

**Step 2: Commit**

```bash
git add src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/types.ts
git commit -m "feat: add event schedule type definitions"
```

---

### Task 2: Property editor manifest and bundle registration

**Files:**
- Create: `src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/manifest.ts`
- Create: `src/UmbracoCommunity.Extensions/Client/src/property-editors/manifest.ts`
- Modify: `src/UmbracoCommunity.Extensions/Client/src/bundle.manifests.ts`

**Step 1: Create the event schedule manifest**

```typescript
// src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/manifest.ts
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "propertyEditorUi",
    alias: "UmbracoCommunity.PropertyEditorUi.EventSchedule",
    name: "Event Schedule",
    element: () => import("./event-schedule-editor.element.js"),
    elementName: "event-schedule-editor",
    meta: {
      label: "Event Schedule",
      icon: "icon-calendar",
      group: "common",
      propertyEditorSchemaAlias: "Umbraco.Plain.Json",
      settings: {
        properties: [
          {
            alias: "venues",
            label: "Venues",
            description: "Define venue names, aliases, and colors. Enter as JSON array: [{\"alias\": \"my-venue\", \"name\": \"My Venue\", \"color\": \"#283a97\"}]",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextArea",
          },
        ],
        defaultData: [
          {
            alias: "venues",
            value: "[{\"alias\": \"venue-1\", \"name\": \"Venue 1\", \"color\": \"#283a97\"}]",
          },
        ],
      },
    },
  },
];
```

**Step 2: Create the property-editors barrel manifest**

```typescript
// src/UmbracoCommunity.Extensions/Client/src/property-editors/manifest.ts
import { manifests as eventSchedule } from "./event-schedule/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [...eventSchedule];
```

**Step 3: Register in the bundle**

Modify `src/UmbracoCommunity.Extensions/Client/src/bundle.manifests.ts` to add the import:

```typescript
import { manifests as dashboards } from "./dashboards/manifest.js";
import { manifests as conditions } from "./conditions/manifest.js";
import { manifests as entityActions } from "./entity-actions/manifest.js";
import { manifests as propertyEditors } from "./property-editors/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...dashboards,
  ...conditions,
  ...entityActions,
  ...propertyEditors,
];
```

**Step 4: Commit**

```bash
git add src/UmbracoCommunity.Extensions/Client/src/property-editors/
git add src/UmbracoCommunity.Extensions/Client/src/bundle.manifests.ts
git commit -m "feat: register event schedule property editor manifest"
```

---

### Task 3: Schedule preview component (shared between editor and frontend)

This is the visual grid that renders the schedule. It will be used both in the backoffice preview and adapted for the frontend. Build it as an internal sub-component of the editor first.

**Files:**
- Create: `src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/event-schedule-preview.element.ts`

**Step 1: Create the preview grid component**

This component renders the time-based grid with days as columns and hours as rows. Events are absolutely positioned within their day column based on start/end times.

```typescript
import {
  LitElement,
  css,
  html,
  nothing,
  customElement,
  property,
} from "@umbraco-cms/backoffice/external/lit";
import type {
  EventScheduleValue,
  EventScheduleVenue,
  EventScheduleEvent,
} from "./types.js";

@customElement("event-schedule-preview")
export class EventSchedulePreviewElement extends LitElement {
  @property({ type: Object })
  schedule: EventScheduleValue | null = null;

  @property({ type: Array })
  venues: EventScheduleVenue[] = [];

  // Height per hour in pixels
  readonly #hourHeight = 60;

  #getVenue(alias: string): EventScheduleVenue | undefined {
    return this.venues.find((v) => v.alias === alias);
  }

  #getEventsForDay(dayIndex: number): EventScheduleEvent[] {
    if (!this.schedule) return [];
    return this.schedule.events
      .filter((e) => e.dayIndex === dayIndex)
      .sort((a, b) => a.startTime.localeCompare(b.startTime));
  }

  #timeToMinutes(time: string): number {
    const [h, m] = time.split(":").map(Number);
    return h * 60 + m;
  }

  #getEventStyle(event: EventScheduleEvent): string {
    if (!this.schedule) return "";
    const startMinutes = this.#timeToMinutes(event.startTime);
    const endMinutes = this.#timeToMinutes(event.endTime);
    const scheduleStartMinutes = this.schedule.settings.startHour * 60;

    const topPx = ((startMinutes - scheduleStartMinutes) / 60) * this.#hourHeight;
    const heightPx = ((endMinutes - startMinutes) / 60) * this.#hourHeight;
    const venue = this.#getVenue(event.venueAlias);
    const bgColor = venue?.color ?? "#283a97";

    // Determine if text should be light or dark based on background luminance
    const isLight = this.#isLightColor(bgColor);
    const textColor = isLight ? "#1b264f" : "#f9f7f4";

    return `top: ${topPx}px; height: ${heightPx}px; background-color: ${bgColor}; color: ${textColor};`;
  }

  #isLightColor(hex: string): boolean {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    return luminance > 0.5;
  }

  #formatHour(hour: number): string {
    const h = hour % 24;
    const ampm = h < 12 ? "AM" : "PM";
    const display = h === 0 ? 12 : h > 12 ? h - 12 : h;
    return `${display.toString().padStart(2, "0")} ${ampm}`;
  }

  render() {
    if (!this.schedule || this.schedule.days.length === 0) {
      return html`<div class="empty">Add days and events to see the preview</div>`;
    }

    const { startHour, endHour } = this.schedule.settings;
    const hours = Array.from({ length: endHour - startHour }, (_, i) => startHour + i);
    const totalHeight = hours.length * this.#hourHeight;

    return html`
      <div class="schedule-grid">
        <div class="grid-header">
          <div class="time-gutter"></div>
          ${this.schedule.days.map(
            (day) => html`<div class="day-header">${day.label}</div>`
          )}
        </div>
        <div class="grid-body" style="height: ${totalHeight}px">
          <div class="time-gutter">
            ${hours.map(
              (hour) => html`
                <div class="hour-label" style="height: ${this.#hourHeight}px">
                  ${this.#formatHour(hour)}
                </div>
              `
            )}
          </div>
          ${this.schedule.days.map((_, dayIndex) => html`
            <div class="day-column">
              ${hours.map(
                () => html`<div class="hour-line" style="height: ${this.#hourHeight}px"></div>`
              )}
              ${this.#getEventsForDay(dayIndex).map(
                (event) => html`
                  <div class="event-block" style=${this.#getEventStyle(event)}>
                    <span class="event-title">${event.title}</span>
                    ${event.subtitle
                      ? html`<span class="event-subtitle">${event.subtitle}</span>`
                      : nothing}
                    ${event.notIncludedInTicket
                      ? html`<span class="event-asterisk">*</span>`
                      : nothing}
                  </div>
                `
              )}
            </div>
          `)}
        </div>
        <div class="legend">
          ${this.venues.map(
            (venue) => html`
              <div class="legend-item">
                <span
                  class="legend-swatch"
                  style="background-color: ${venue.color}"
                ></span>
                ${venue.name}
              </div>
            `
          )}
        </div>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: var(--uui-font-family, sans-serif);
      font-size: 12px;
    }

    .empty {
      padding: var(--uui-size-space-4, 1rem);
      color: var(--uui-color-text-alt, #666);
      text-align: center;
      font-style: italic;
    }

    .schedule-grid {
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 6px;
      overflow: hidden;
    }

    .grid-header {
      display: flex;
      border-bottom: 2px solid var(--uui-color-border, #e0e0e0);
    }

    .time-gutter {
      width: 60px;
      flex-shrink: 0;
    }

    .day-header {
      flex: 1;
      text-align: center;
      padding: 8px 4px;
      font-weight: 600;
      border-left: 1px solid var(--uui-color-border, #e0e0e0);
    }

    .grid-body {
      display: flex;
      position: relative;
    }

    .hour-label {
      display: flex;
      align-items: flex-start;
      justify-content: center;
      padding-top: 2px;
      color: var(--uui-color-text-alt, #666);
      font-size: 11px;
      box-sizing: border-box;
      border-bottom: 1px solid var(--uui-color-border-standalone, #f0f0f0);
    }

    .day-column {
      flex: 1;
      position: relative;
      border-left: 1px solid var(--uui-color-border, #e0e0e0);
    }

    .hour-line {
      box-sizing: border-box;
      border-bottom: 1px solid var(--uui-color-border-standalone, #f0f0f0);
    }

    .event-block {
      position: absolute;
      left: 3px;
      right: 3px;
      border-radius: 4px;
      padding: 4px 6px;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      min-height: 0;
    }

    .event-title {
      font-weight: 600;
      font-size: 11px;
      line-height: 1.2;
    }

    .event-subtitle {
      font-size: 10px;
      font-style: italic;
      opacity: 0.85;
    }

    .event-asterisk {
      font-size: 10px;
      opacity: 0.7;
    }

    .legend {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      padding: 8px 12px;
      border-top: 1px solid var(--uui-color-border, #e0e0e0);
      font-size: 11px;
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .legend-swatch {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 2px;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "event-schedule-preview": EventSchedulePreviewElement;
  }
}
```

**Step 2: Commit**

```bash
git add src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/event-schedule-preview.element.ts
git commit -m "feat: add event schedule preview grid component"
```

---

### Task 4: Property editor UI element (form + preview)

**Files:**
- Create: `src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/event-schedule-editor.element.ts`

**Step 1: Create the property editor UI element**

This is the main editor registered with Umbraco. It implements `UmbPropertyEditorUiElement`, reads configuration (venues) from the data type settings, and stores the schedule as a JSON object via `Umbraco.Plain.Json`.

Key patterns:
- `value` is `@property({ type: Object })` since `Umbraco.Plain.Json` passes objects not strings
- Dispatches `UmbChangeEvent` on every change to persist
- Reads `venues` config from `config.getValueByAlias('venues')` as a JSON string to parse
- Form panel on the left, preview panel on the right

```typescript
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
  EventScheduleDay,
  EventScheduleEvent,
} from "./types.js";
import { DEFAULT_SCHEDULE_VALUE } from "./types.js";
import "./event-schedule-preview.element.js";

@customElement("event-schedule-editor")
export default class EventScheduleEditorElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  @property({ type: Object })
  public value: EventScheduleValue | null = null;

  @state()
  private _venues: EventScheduleVenue[] = [];

  @state()
  private _editingEvent: EventScheduleEvent | null = null;

  @state()
  private _editingDayIndex: number | null = null;

  @state()
  private _editingEventIsNew = false;

  @property({ attribute: false })
  public set config(config: UmbPropertyEditorConfigCollection) {
    const venuesRaw = config.getValueByAlias<string>("venues");
    if (venuesRaw) {
      try {
        this._venues = JSON.parse(venuesRaw);
      } catch {
        this._venues = [];
      }
    }
  }

  get #schedule(): EventScheduleValue {
    return this.value ?? { ...DEFAULT_SCHEDULE_VALUE };
  }

  #updateValue(schedule: EventScheduleValue) {
    this.value = { ...schedule };
    this.dispatchEvent(new UmbChangeEvent());
  }

  // --- Day management ---

  #onAddDay() {
    const schedule = { ...this.#schedule };
    const date = new Date();
    // Default to day after last day, or today
    if (schedule.days.length > 0) {
      const lastDate = new Date(schedule.days[schedule.days.length - 1].date);
      lastDate.setDate(lastDate.getDate() + 1);
      date.setTime(lastDate.getTime());
    }
    const isoDate = date.toISOString().slice(0, 10);
    const label = date.toLocaleDateString("en-GB", { day: "2-digit", month: "short" });

    schedule.days = [...schedule.days, { date: isoDate, label }];
    this.#updateValue(schedule);
  }

  #onRemoveDay(dayIndex: number) {
    const schedule = { ...this.#schedule };
    schedule.days = schedule.days.filter((_, i) => i !== dayIndex);
    // Re-index events: remove events for deleted day, adjust dayIndex for later days
    schedule.events = schedule.events
      .filter((e) => e.dayIndex !== dayIndex)
      .map((e) => ({
        ...e,
        dayIndex: e.dayIndex > dayIndex ? e.dayIndex - 1 : e.dayIndex,
      }));
    this.#updateValue(schedule);
  }

  #onDayDateChange(dayIndex: number, e: Event) {
    const input = e.target as HTMLInputElement;
    const date = new Date(input.value + "T00:00:00");
    const schedule = { ...this.#schedule };
    schedule.days = schedule.days.map((d, i) =>
      i === dayIndex
        ? {
            date: input.value,
            label: date.toLocaleDateString("en-GB", { day: "2-digit", month: "short" }),
          }
        : d
    );
    this.#updateValue(schedule);
  }

  // --- Event management ---

  #onAddEvent(dayIndex: number) {
    const schedule = this.#schedule;
    const newEvent: EventScheduleEvent = {
      id: `evt-${Date.now()}`,
      title: "",
      subtitle: "",
      dayIndex,
      startTime: `${String(schedule.settings.startHour).padStart(2, "0")}:00`,
      endTime: `${String(Math.min(schedule.settings.startHour + 1, schedule.settings.endHour)).padStart(2, "0")}:00`,
      venueAlias: this._venues.length > 0 ? this._venues[0].alias : "",
      notIncludedInTicket: false,
    };
    this._editingEvent = newEvent;
    this._editingDayIndex = dayIndex;
    this._editingEventIsNew = true;
  }

  #onEditEvent(event: EventScheduleEvent) {
    this._editingEvent = { ...event };
    this._editingDayIndex = event.dayIndex;
    this._editingEventIsNew = false;
  }

  #onRemoveEvent(eventId: string) {
    const schedule = { ...this.#schedule };
    schedule.events = schedule.events.filter((e) => e.id !== eventId);
    this.#updateValue(schedule);
  }

  #onSaveEvent() {
    if (!this._editingEvent) return;
    const schedule = { ...this.#schedule };
    if (this._editingEventIsNew) {
      schedule.events = [...schedule.events, this._editingEvent];
    } else {
      schedule.events = schedule.events.map((e) =>
        e.id === this._editingEvent!.id ? this._editingEvent! : e
      );
    }
    this.#updateValue(schedule);
    this._editingEvent = null;
    this._editingDayIndex = null;
  }

  #onCancelEdit() {
    this._editingEvent = null;
    this._editingDayIndex = null;
  }

  #onEventFieldChange(field: keyof EventScheduleEvent, e: Event) {
    if (!this._editingEvent) return;
    const target = e.target as HTMLInputElement;
    const value = target.type === "checkbox" ? target.checked : target.value;
    this._editingEvent = { ...this._editingEvent, [field]: value };
  }

  // --- Settings ---

  #onSettingChange(field: keyof EventScheduleValue["settings"], e: Event) {
    const target = e.target as HTMLInputElement;
    const schedule = { ...this.#schedule };
    schedule.settings = { ...schedule.settings, [field]: Number(target.value) };
    this.#updateValue(schedule);
  }

  // --- Render ---

  #renderSettings() {
    const s = this.#schedule.settings;
    return html`
      <details class="settings-panel">
        <summary>Schedule Settings</summary>
        <div class="settings-fields">
          <label>
            Start hour
            <input type="number" min="0" max="23" .value=${String(s.startHour)}
              @change=${(e: Event) => this.#onSettingChange("startHour", e)} />
          </label>
          <label>
            End hour
            <input type="number" min="1" max="24" .value=${String(s.endHour)}
              @change=${(e: Event) => this.#onSettingChange("endHour", e)} />
          </label>
          <label>
            Granularity (min)
            <input type="number" min="5" max="60" step="5" .value=${String(s.granularityMinutes)}
              @change=${(e: Event) => this.#onSettingChange("granularityMinutes", e)} />
          </label>
        </div>
      </details>
    `;
  }

  #renderEventForm() {
    if (!this._editingEvent) return nothing;
    const evt = this._editingEvent;
    return html`
      <div class="event-form">
        <h4>${this._editingEventIsNew ? "Add Event" : "Edit Event"}</h4>
        <label>
          Title
          <input type="text" .value=${evt.title}
            @input=${(e: Event) => this.#onEventFieldChange("title", e)} />
        </label>
        <label>
          Subtitle
          <input type="text" .value=${evt.subtitle}
            @input=${(e: Event) => this.#onEventFieldChange("subtitle", e)} />
        </label>
        <label>
          Start time
          <input type="time" .value=${evt.startTime}
            @change=${(e: Event) => this.#onEventFieldChange("startTime", e)} />
        </label>
        <label>
          End time
          <input type="time" .value=${evt.endTime}
            @change=${(e: Event) => this.#onEventFieldChange("endTime", e)} />
        </label>
        <label>
          Venue
          <select @change=${(e: Event) => this.#onEventFieldChange("venueAlias", e)}>
            ${this._venues.map(
              (v) =>
                html`<option value=${v.alias} ?selected=${v.alias === evt.venueAlias}>
                  ${v.name}
                </option>`
            )}
          </select>
        </label>
        <label class="checkbox-label">
          <input type="checkbox" .checked=${evt.notIncludedInTicket}
            @change=${(e: Event) => this.#onEventFieldChange("notIncludedInTicket", e)} />
          Not included in ticket price
        </label>
        <div class="form-actions">
          <uui-button look="primary" @click=${this.#onSaveEvent}>Save</uui-button>
          <uui-button look="secondary" @click=${this.#onCancelEdit}>Cancel</uui-button>
        </div>
      </div>
    `;
  }

  #renderDays() {
    return html`
      ${this.#schedule.days.map((day, dayIndex) => html`
        <div class="day-section">
          <div class="day-header-row">
            <input type="date" .value=${day.date}
              @change=${(e: Event) => this.#onDayDateChange(dayIndex, e)} />
            <strong>${day.label}</strong>
            <uui-button look="secondary" compact @click=${() => this.#onRemoveDay(dayIndex)}>
              <uui-icon name="icon-trash"></uui-icon>
            </uui-button>
          </div>
          <div class="day-events">
            ${this.#schedule.events
              .filter((e) => e.dayIndex === dayIndex)
              .sort((a, b) => a.startTime.localeCompare(b.startTime))
              .map((evt) => html`
                <div class="event-row">
                  <span class="event-time">${evt.startTime}-${evt.endTime}</span>
                  <span class="event-name">${evt.title}</span>
                  <span class="event-venue-badge"
                    style="background-color: ${this._venues.find((v) => v.alias === evt.venueAlias)?.color ?? "#ccc"}"
                  ></span>
                  <uui-button look="secondary" compact
                    @click=${() => this.#onEditEvent(evt)}>
                    <uui-icon name="icon-edit"></uui-icon>
                  </uui-button>
                  <uui-button look="secondary" compact
                    @click=${() => this.#onRemoveEvent(evt.id)}>
                    <uui-icon name="icon-trash"></uui-icon>
                  </uui-button>
                </div>
              `)}
            ${this._editingDayIndex === dayIndex && this._editingEvent
              ? this.#renderEventForm()
              : html`
                <uui-button look="outline" compact
                  @click=${() => this.#onAddEvent(dayIndex)}>
                  <uui-icon name="icon-add"></uui-icon> Add Event
                </uui-button>
              `}
          </div>
        </div>
      `)}
    `;
  }

  render() {
    return html`
      <div class="editor-layout">
        <div class="form-panel">
          ${this.#renderSettings()}
          <div class="days-section">
            <div class="days-header">
              <h3>Days</h3>
              <uui-button look="primary" compact @click=${this.#onAddDay}>
                <uui-icon name="icon-add"></uui-icon> Add Day
              </uui-button>
            </div>
            ${this.#renderDays()}
          </div>
        </div>
        <div class="preview-panel">
          <h3>Preview</h3>
          <event-schedule-preview
            .schedule=${this.#schedule}
            .venues=${this._venues}
          ></event-schedule-preview>
        </div>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    .editor-layout {
      display: flex;
      gap: var(--uui-size-space-5, 1.25rem);
    }

    @media (max-width: 900px) {
      .editor-layout {
        flex-direction: column;
      }
    }

    .form-panel {
      flex: 1;
      min-width: 0;
    }

    .preview-panel {
      flex: 1;
      min-width: 0;
    }

    .preview-panel h3,
    .days-header h3 {
      margin: 0 0 var(--uui-size-space-3, 0.75rem);
      font-size: var(--uui-type-h5-size, 1rem);
    }

    .days-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--uui-size-space-3, 0.75rem);
    }

    .settings-panel {
      margin-bottom: var(--uui-size-space-4, 1rem);
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 6px;
      padding: var(--uui-size-space-3, 0.75rem);
    }

    .settings-panel summary {
      cursor: pointer;
      font-weight: 600;
    }

    .settings-fields {
      display: flex;
      gap: var(--uui-size-space-4, 1rem);
      margin-top: var(--uui-size-space-3, 0.75rem);
    }

    .settings-fields label {
      display: flex;
      flex-direction: column;
      gap: 4px;
      font-size: 13px;
    }

    .settings-fields input {
      width: 80px;
      padding: 4px 8px;
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 4px;
    }

    .day-section {
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 6px;
      padding: var(--uui-size-space-3, 0.75rem);
      margin-bottom: var(--uui-size-space-3, 0.75rem);
    }

    .day-header-row {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3, 0.75rem);
      margin-bottom: var(--uui-size-space-2, 0.5rem);
    }

    .day-header-row input[type="date"] {
      padding: 4px 8px;
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 4px;
    }

    .event-row {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-2, 0.5rem);
      padding: 4px 0;
      border-bottom: 1px solid var(--uui-color-border-standalone, #f0f0f0);
    }

    .event-time {
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
      min-width: 90px;
    }

    .event-name {
      flex: 1;
      font-size: 13px;
    }

    .event-venue-badge {
      width: 12px;
      height: 12px;
      border-radius: 2px;
      flex-shrink: 0;
    }

    .event-form {
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 6px;
      padding: var(--uui-size-space-3, 0.75rem);
      margin-top: var(--uui-size-space-2, 0.5rem);
      background: var(--uui-color-surface-alt, #fafafa);
    }

    .event-form h4 {
      margin: 0 0 var(--uui-size-space-2, 0.5rem);
      font-size: 14px;
    }

    .event-form label {
      display: flex;
      flex-direction: column;
      gap: 4px;
      margin-bottom: var(--uui-size-space-2, 0.5rem);
      font-size: 13px;
    }

    .event-form input[type="text"],
    .event-form input[type="time"],
    .event-form select {
      padding: 6px 8px;
      border: 1px solid var(--uui-color-border, #e0e0e0);
      border-radius: 4px;
      font-size: 13px;
    }

    .checkbox-label {
      flex-direction: row !important;
      align-items: center;
    }

    .form-actions {
      display: flex;
      gap: var(--uui-size-space-2, 0.5rem);
      margin-top: var(--uui-size-space-3, 0.75rem);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "event-schedule-editor": EventScheduleEditorElement;
  }
}
```

**Step 2: Build and verify**

```bash
cd src/UmbracoCommunity.Extensions/Client
npm run build
```

Expected: Build succeeds with no TypeScript errors.

**Step 3: Commit**

```bash
git add src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/event-schedule-editor.element.ts
git commit -m "feat: add event schedule property editor UI with form and preview"
```

---

### Task 5: Manual test in backoffice

**Step 1: Start the application**

Terminal 1:
```bash
cd src/UmbracoCommunity.Web.UI
dotnet run
```

Terminal 2:
```bash
cd src/UmbracoCommunity.Extensions/Client
npm run watch
```

**Step 2: Create a data type in the backoffice**

1. Go to Settings > Data Types
2. Click "Create" > "New Data Type"
3. Name: "Event Schedule"
4. Select property editor: "Event Schedule" (look for icon-calendar)
5. In the "Venues" configuration field, enter:
   ```json
   [{"alias": "cph-conference", "name": "CPH Conference", "color": "#f4c7c3"}, {"alias": "oksnehallen", "name": "Øksnehallen", "color": "#283a97"}, {"alias": "meatpacking", "name": "Copenhagen Meatpacking District", "color": "#1b264f"}]
   ```
6. Save

**Step 3: Add property to a document type**

1. Go to Settings > Document Types
2. Open or create a test document type
3. Add a property using the "Event Schedule" data type
4. Save

**Step 4: Test the editor**

1. Create or edit content using that document type
2. Verify: settings panel opens/closes
3. Verify: "Add Day" creates a day entry with date picker
4. Verify: "Add Event" shows the event form with all fields
5. Verify: Save/cancel works for events
6. Verify: Preview grid updates live as events are added
7. Verify: Venue colors show correctly in preview
8. Verify: Save content and reload - data persists

**Step 5: Fix any issues found during testing, commit**

---

### Task 6: Frontend display component

**Files:**
- Create: `src/UmbracoCommunity.StaticAssets/src/components/event-schedule/event-schedule.element.ts`
- Create: `src/UmbracoCommunity.StaticAssets/src/components/event-schedule/index.ts`
- Modify: `src/UmbracoCommunity.StaticAssets/src/components/index.ts`

**Step 1: Create the frontend display component**

This is the public-facing component. It follows the same patterns as `sessionize-program.element.ts` - uses Lit, CSS custom properties from the design system, and is responsive.

```typescript
// src/UmbracoCommunity.StaticAssets/src/components/event-schedule/event-schedule.element.ts
import { LitElement, css, html, nothing } from "lit";
import { customElement, property, state } from "lit/decorators.js";

interface Venue {
  alias: string;
  name: string;
  color: string;
}

interface Day {
  date: string;
  label: string;
}

interface ScheduleEvent {
  id: string;
  title: string;
  subtitle: string;
  dayIndex: number;
  startTime: string;
  endTime: string;
  venueAlias: string;
  notIncludedInTicket: boolean;
}

interface ScheduleSettings {
  startHour: number;
  endHour: number;
  granularityMinutes: number;
}

interface ScheduleData {
  settings: ScheduleSettings;
  days: Day[];
  events: ScheduleEvent[];
}

const elementName = "dc-event-schedule";

@customElement(elementName)
export class EventScheduleElement extends LitElement {
  @property({ type: String })
  data = "";

  @property({ type: String })
  venues = "";

  @state()
  private _schedule: ScheduleData | null = null;

  @state()
  private _venues: Venue[] = [];

  @state()
  private _selectedDayIndex = 0;

  @state()
  private _isMobile = false;

  #resizeObserver?: ResizeObserver;

  readonly #hourHeight = 90;

  connectedCallback() {
    super.connectedCallback();
    this.#parseData();
    this.#resizeObserver = new ResizeObserver(() => {
      this._isMobile = this.offsetWidth < 768;
    });
    this.#resizeObserver.observe(this);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#resizeObserver?.disconnect();
  }

  willUpdate(changed: Map<string, unknown>) {
    if (changed.has("data") || changed.has("venues")) {
      this.#parseData();
    }
  }

  #parseData() {
    try {
      this._schedule = this.data ? JSON.parse(this.data) : null;
    } catch {
      this._schedule = null;
    }
    try {
      this._venues = this.venues ? JSON.parse(this.venues) : [];
    } catch {
      this._venues = [];
    }
  }

  #getVenue(alias: string): Venue | undefined {
    return this._venues.find((v) => v.alias === alias);
  }

  #getEventsForDay(dayIndex: number): ScheduleEvent[] {
    if (!this._schedule) return [];
    return this._schedule.events
      .filter((e) => e.dayIndex === dayIndex)
      .sort((a, b) => a.startTime.localeCompare(b.startTime));
  }

  #timeToMinutes(time: string): number {
    const [h, m] = time.split(":").map(Number);
    return h * 60 + m;
  }

  #formatTime(time: string): string {
    const [h, m] = time.split(":").map(Number);
    const ampm = h < 12 ? "AM" : "PM";
    const display = h === 0 ? 12 : h > 12 ? h - 12 : h;
    return `${display}:${String(m).padStart(2, "0")} ${ampm}`;
  }

  #formatHour(hour: number): string {
    const h = hour % 24;
    const ampm = h < 12 ? "AM" : "PM";
    const display = h === 0 ? 12 : h > 12 ? h - 12 : h;
    return `${String(display).padStart(2, "0")} ${ampm}`;
  }

  #isLightColor(hex: string): boolean {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.5;
  }

  #getEventStyle(event: ScheduleEvent): string {
    if (!this._schedule) return "";
    const startMin = this.#timeToMinutes(event.startTime);
    const endMin = this.#timeToMinutes(event.endTime);
    const scheduleStartMin = this._schedule.settings.startHour * 60;

    const topPx = ((startMin - scheduleStartMin) / 60) * this.#hourHeight;
    const heightPx = ((endMin - startMin) / 60) * this.#hourHeight;
    const venue = this.#getVenue(event.venueAlias);
    const bg = venue?.color ?? "#283a97";
    const text = this.#isLightColor(bg) ? "#1b264f" : "#f9f7f4";

    return `top: ${topPx}px; height: ${heightPx}px; background-color: ${bg}; color: ${text};`;
  }

  #renderDesktop() {
    if (!this._schedule) return nothing;
    const { startHour, endHour } = this._schedule.settings;
    const hours = Array.from({ length: endHour - startHour }, (_, i) => startHour + i);
    const totalHeight = hours.length * this.#hourHeight;

    return html`
      <div class="schedule-desktop">
        <div class="grid-header">
          <div class="time-gutter"></div>
          ${this._schedule.days.map(
            (day) => html`<div class="day-header">${day.label}</div>`
          )}
        </div>
        <div class="grid-body" style="height: ${totalHeight}px">
          <div class="time-gutter">
            ${hours.map(
              (hour) => html`
                <div class="hour-label" style="height: ${this.#hourHeight}px">
                  <span class="hour-text">${this.#formatHour(hour)}</span>
                </div>
              `
            )}
          </div>
          ${this._schedule.days.map(
            (_, dayIndex) => html`
              <div class="day-column">
                ${hours.map(
                  () => html`<div class="hour-line" style="height: ${this.#hourHeight}px"></div>`
                )}
                ${this.#getEventsForDay(dayIndex).map(
                  (event) => html`
                    <div
                      class="event-block"
                      style=${this.#getEventStyle(event)}
                      aria-label="${event.title}, ${this.#formatTime(event.startTime)} to ${this.#formatTime(event.endTime)}"
                    >
                      <span class="event-title">${event.title}</span>
                      ${event.subtitle
                        ? html`<span class="event-subtitle">${event.subtitle}</span>`
                        : nothing}
                      ${event.notIncludedInTicket
                        ? html`<span class="event-asterisk">*</span>`
                        : nothing}
                    </div>
                  `
                )}
              </div>
            `
          )}
        </div>
      </div>
    `;
  }

  #renderMobile() {
    if (!this._schedule) return nothing;

    return html`
      <div class="schedule-mobile">
        <div class="day-tabs" role="tablist">
          ${this._schedule.days.map(
            (day, i) => html`
              <button
                class="day-tab ${i === this._selectedDayIndex ? "active" : ""}"
                role="tab"
                aria-selected=${i === this._selectedDayIndex}
                @click=${() => (this._selectedDayIndex = i)}
              >
                ${day.label}
              </button>
            `
          )}
        </div>
        <div class="day-events-list" role="tabpanel">
          ${this.#getEventsForDay(this._selectedDayIndex).map((event) => {
            const venue = this.#getVenue(event.venueAlias);
            const bg = venue?.color ?? "#283a97";
            const text = this.#isLightColor(bg) ? "#1b264f" : "#f9f7f4";
            return html`
              <div class="mobile-event" style="border-left: 4px solid ${bg}">
                <div class="mobile-event-time">
                  ${this.#formatTime(event.startTime)} - ${this.#formatTime(event.endTime)}
                </div>
                <div class="mobile-event-title">${event.title}</div>
                ${event.subtitle
                  ? html`<div class="mobile-event-subtitle">${event.subtitle}</div>`
                  : nothing}
                <div class="mobile-event-venue">
                  <span class="venue-dot" style="background-color: ${bg}; color: ${text}"></span>
                  ${venue?.name ?? "Unknown venue"}
                  ${event.notIncludedInTicket ? html`<span>*</span>` : nothing}
                </div>
              </div>
            `;
          })}
        </div>
      </div>
    `;
  }

  #renderLegend() {
    const hasNotIncluded = this._schedule?.events.some((e) => e.notIncludedInTicket);
    return html`
      <div class="legend">
        ${hasNotIncluded
          ? html`<span class="legend-note">*not included in the price of the ticket</span>`
          : nothing}
        ${this._venues.map(
          (v) => html`
            <div class="legend-item">
              <span class="legend-swatch" style="background-color: ${v.color}"></span>
              ${v.name}
            </div>
          `
        )}
      </div>
    `;
  }

  render() {
    if (!this._schedule || this._schedule.days.length === 0) return nothing;

    return html`
      ${this._isMobile ? this.#renderMobile() : this.#renderDesktop()}
      ${this.#renderLegend()}
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: inherit;
    }

    /* Desktop Grid */
    .schedule-desktop {
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-radius: var(--border-radius-lg, 8px);
      overflow: hidden;
    }

    .grid-header {
      display: flex;
      border-bottom: 2px solid var(--color-grey-light, #e5e7eb);
    }

    .time-gutter {
      width: 70px;
      flex-shrink: 0;
    }

    .day-header {
      flex: 1;
      text-align: center;
      padding: var(--unit-sm, 0.75rem) var(--unit-xs, 0.5rem);
      font-weight: 600;
      font-size: 0.95rem;
      border-left: 1px solid var(--color-grey-light, #e5e7eb);
      color: var(--color-identity-dark, #1b264f);
    }

    .grid-body {
      display: flex;
      position: relative;
    }

    .hour-label {
      display: flex;
      align-items: flex-start;
      justify-content: center;
      padding-top: 4px;
      box-sizing: border-box;
      border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .hour-text {
      font-size: 0.8rem;
      color: var(--color-identity-dark, #1b264f);
      opacity: 0.7;
      line-height: 1;
    }

    .day-column {
      flex: 1;
      position: relative;
      border-left: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .hour-line {
      box-sizing: border-box;
      border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .event-block {
      position: absolute;
      left: 4px;
      right: 4px;
      border-radius: var(--border-radius, 6px);
      padding: var(--unit-xs, 0.5rem);
      overflow: hidden;
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
    }

    .event-title {
      font-weight: 600;
      font-size: 0.85rem;
      line-height: 1.25;
    }

    .event-subtitle {
      font-size: 0.75rem;
      font-style: italic;
      opacity: 0.85;
      margin-top: 2px;
    }

    .event-asterisk {
      margin-top: auto;
      font-size: 0.7rem;
      opacity: 0.7;
      align-self: flex-end;
    }

    /* Mobile */
    .schedule-mobile {
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-radius: var(--border-radius-lg, 8px);
      overflow: hidden;
    }

    .day-tabs {
      display: flex;
      overflow-x: auto;
      border-bottom: 2px solid var(--color-grey-light, #e5e7eb);
    }

    .day-tab {
      flex: 1;
      min-width: max-content;
      padding: var(--unit-sm, 0.75rem) var(--unit, 1rem);
      border: none;
      background: none;
      cursor: pointer;
      font-weight: 600;
      font-size: 0.9rem;
      color: var(--color-identity-dark, #1b264f);
      opacity: 0.6;
      transition: opacity 0.2s;
    }

    .day-tab.active {
      opacity: 1;
      border-bottom: 3px solid var(--color-identity-blue, #283a97);
    }

    .day-events-list {
      padding: var(--unit-sm, 0.75rem);
      display: flex;
      flex-direction: column;
      gap: var(--unit-sm, 0.75rem);
    }

    .mobile-event {
      padding: var(--unit-sm, 0.75rem);
      border-radius: var(--border-radius, 6px);
      background: var(--color-white, #f9f7f4);
      border: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .mobile-event-time {
      font-size: 0.8rem;
      color: var(--color-identity-dark, #1b264f);
      opacity: 0.7;
      margin-bottom: 4px;
    }

    .mobile-event-title {
      font-weight: 600;
      font-size: 0.95rem;
      color: var(--color-identity-dark, #1b264f);
    }

    .mobile-event-subtitle {
      font-size: 0.85rem;
      font-style: italic;
      color: var(--color-identity-dark, #1b264f);
      opacity: 0.7;
      margin-top: 2px;
    }

    .mobile-event-venue {
      display: flex;
      align-items: center;
      gap: 6px;
      margin-top: var(--unit-xs, 0.5rem);
      font-size: 0.8rem;
      color: var(--color-identity-dark, #1b264f);
      opacity: 0.8;
    }

    .venue-dot {
      width: 10px;
      height: 10px;
      border-radius: 2px;
      flex-shrink: 0;
    }

    /* Legend */
    .legend {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--unit, 1rem);
      padding: var(--unit, 1rem) 0;
      font-size: 0.85rem;
      color: var(--color-identity-dark, #1b264f);
    }

    .legend-note {
      font-style: italic;
      opacity: 0.7;
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
    }

    @media (min-width: 768px) and (max-width: 1023px) {
      .day-header {
        font-size: 0.85rem;
        padding: var(--unit-xs, 0.5rem);
      }

      .event-title {
        font-size: 0.75rem;
      }

      .event-subtitle {
        font-size: 0.65rem;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: EventScheduleElement;
  }
}
```

**Step 2: Create the barrel export**

```typescript
// src/UmbracoCommunity.StaticAssets/src/components/event-schedule/index.ts
export * from "./event-schedule.element.js";
```

**Step 3: Register in components/index.ts**

Add this line to `src/UmbracoCommunity.StaticAssets/src/components/index.ts`:

```typescript
export * from "./event-schedule/index.js";
```

**Step 4: Verify frontend build**

```bash
cd src/UmbracoCommunity.StaticAssets
npx tsc --noEmit
```

Expected: No TypeScript errors.

**Step 5: Commit**

```bash
git add src/UmbracoCommunity.StaticAssets/src/components/event-schedule/
git add src/UmbracoCommunity.StaticAssets/src/components/index.ts
git commit -m "feat: add frontend event schedule display component"
```

---

### Task 7: Razor partial view for rendering

**Files:**
- Create: `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/EventScheduleBlock.cshtml`

**Step 1: Determine the exact Razor integration**

Before creating the view, check how the schedule property will be accessed. The property stores JSON via `Umbraco.Plain.Json`, so in Razor it will be available as a raw JSON string on the content model. The exact approach depends on which document type and block type the property is added to.

For a Block Grid item (following the existing pattern from `ProgramBlock.cshtml`), create a partial:

```html
@using Umbraco.Cms.Web.Common
@{
    // Access the schedule JSON value from the block content
    // The property alias will match whatever the editor names it
    var scheduleJson = Model?.Content?.Value<string>("eventSchedule") ?? "";
    var venuesJson = ""; // Venues come from data type config - need to pass separately

    // Note: Venue config is stored in data type settings, not on content.
    // For the frontend, venues should either be embedded in the JSON value
    // or passed as a separate attribute. The simplest approach is to include
    // venues in the schedule JSON value itself during save.
}

@if (!string.IsNullOrWhiteSpace(scheduleJson))
{
    <dc-event-schedule data='@Html.Raw(scheduleJson)'></dc-event-schedule>
}
```

> **Important implementation note:** The venues are stored in data type configuration, not on the content node. The simplest solution is to embed the venue list into the JSON value when saving (add a `venues` array to the `EventScheduleValue` type). This way the frontend component gets everything it needs from a single `data` attribute. Update the types and editor to include venues in the saved value.

**Step 2: Update types to include venues in saved value**

Modify `src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/types.ts` to add venues to the stored value:

```typescript
export interface EventScheduleValue {
  settings: EventScheduleSettings;
  days: EventScheduleDay[];
  events: EventScheduleEvent[];
  venues: EventScheduleVenue[]; // Include venues in saved value for frontend rendering
}

export const DEFAULT_SCHEDULE_VALUE: EventScheduleValue = {
  settings: {
    startHour: 6,
    endHour: 24,
    granularityMinutes: 30,
  },
  days: [],
  events: [],
  venues: [],
};
```

Then update the editor element's `#updateValue` method to always include the current venues in the saved value:

```typescript
#updateValue(schedule: EventScheduleValue) {
  this.value = { ...schedule, venues: this._venues };
  this.dispatchEvent(new UmbChangeEvent());
}
```

And update the frontend component to read venues from the data attribute instead of a separate attribute:

```typescript
// In the frontend component, remove the separate venues property
// and read venues from the schedule data itself:

#parseData() {
  try {
    const parsed = this.data ? JSON.parse(this.data) : null;
    this._schedule = parsed;
    this._venues = parsed?.venues ?? [];
  } catch {
    this._schedule = null;
    this._venues = [];
  }
}
```

The Razor partial simplifies to:

```html
@{
    var scheduleJson = Model?.Content?.Value<string>("eventSchedule") ?? "";
}

@if (!string.IsNullOrWhiteSpace(scheduleJson))
{
    <dc-event-schedule data='@Html.Raw(scheduleJson)'></dc-event-schedule>
}
```

**Step 3: Commit**

```bash
git add src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/types.ts
git add src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/event-schedule-editor.element.ts
git add src/UmbracoCommunity.StaticAssets/src/components/event-schedule/event-schedule.element.ts
git add src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/EventScheduleBlock.cshtml
git commit -m "feat: embed venues in schedule value and add Razor partial"
```

---

### Task 8: End-to-end testing

**Step 1: Build everything**

Terminal 1:
```bash
cd src/UmbracoCommunity.Extensions/Client
npm run build
```

Terminal 2:
```bash
cd src/UmbracoCommunity.StaticAssets
npm run build
```

Terminal 3:
```bash
cd src/UmbracoCommunity.Web.UI
dotnet run
```

**Step 2: Backoffice verification**

1. Create/edit the Event Schedule data type with venue configuration
2. Add property to a document type
3. Create content with several days and events
4. Verify preview grid shows correctly in editor
5. Save and reload - verify data persists
6. Verify all CRUD operations: add/edit/remove days and events

**Step 3: Frontend verification**

1. Navigate to the page with the event schedule on the public site
2. Verify desktop grid renders with correct layout
3. Resize browser below 768px - verify mobile view shows day tabs + event cards
4. Verify venue colors and legend display correctly
5. Verify "not included in ticket" asterisks show

**Step 4: Fix any issues, commit**

```bash
git add -u
git commit -m "fix: resolve issues found during end-to-end testing"
```
