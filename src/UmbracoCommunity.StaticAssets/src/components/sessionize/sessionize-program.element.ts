import { LitElement, css, html, nothing, PropertyValues } from "lit";
import { customElement, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import {
  SessionizeService,
  SessionizeSchedule,
  SessionizeSession,
  SessionizeSpeaker,
  SessionizeTimeSlot,
  SessionizeRoom,
  SessionizeCategory,
  SessionizeCategoryItem,
} from "../../services/sessionize.service.js";
import { DcDialogHandler } from "../dialog/dialog.handler.js";
import { SessionizeSessionDialogElement } from "./sessionize-session-dialog.element.js";

interface CategoryDropdown {
  id: number;
  title: string;
  items: { id: number; name: string }[];
}

interface SelectedFilter {
  id: number;
  name: string;
  categoryTitle: string;
}

const elementName = "dc-sessionize-program";

// Default timezone (Copenhagen for CodeGarden)
const DEFAULT_TIMEZONE = "Europe/Copenhagen";

// Available timezones for the dropdown with UTC offsets (ordered low to high)
const TIMEZONE_OPTIONS = [
  { value: "America/Los_Angeles", label: "Los Angeles (UTC-7)" },
  { value: "America/Denver", label: "Denver (UTC-6)" },
  { value: "America/Chicago", label: "Chicago (UTC-5)" },
  { value: "America/New_York", label: "New York (UTC-4)" },
  { value: "UTC", label: "UTC" },
  { value: "Europe/London", label: "London (UTC+1)" },
  { value: "Europe/Copenhagen", label: "Copenhagen (UTC+2)" },
  { value: "Europe/Paris", label: "Paris (UTC+2)" },
  { value: "Europe/Berlin", label: "Berlin (UTC+2)" },
  { value: "Asia/Singapore", label: "Singapore (UTC+8)" },
  { value: "Asia/Tokyo", label: "Tokyo (UTC+9)" },
  { value: "Australia/Sydney", label: "Sydney (UTC+10)" },
];

@customElement(elementName)
export class SessionizeProgramElement extends LitElement {
  @state()
  private _schedule: SessionizeSchedule[] = [];

  @state()
  private _categories: SessionizeCategory[] = [];

  @state()
  private _categoryDropdowns: CategoryDropdown[] = [];

  @state()
  private _selectedFilters: SelectedFilter[] = [];

  @state()
  private _selectedDayIndex = 0;

  @state()
  private _selectedTimezone = DEFAULT_TIMEZONE;

  @state()
  private _loading = true;

  @state()
  private _error: string | null = null;

  // Cached/computed values - updated in willUpdate when dependencies change
  #filteredSessionsByRoom = new Map<number, SessionizeSession[]>();
  #filteredSessionIds = new Set<string>();
  #activeRooms: SessionizeRoom[] = [];

  #dialogHandler = new DcDialogHandler();

  // Sticky bar visibility
  @state()
  private _showStickyBar = false;

  @state()
  private _isProgramInView = true;

  #controlsObserver?: IntersectionObserver;
  #programEndObserver?: IntersectionObserver;
  #controlsRef?: HTMLElement;
  #programContainerRef?: HTMLElement;

  connectedCallback() {
    super.connectedCallback();
    this.#loadSchedule();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#controlsObserver?.disconnect();
    this.#programEndObserver?.disconnect();
  }

  #setupControlsObserver() {
    // Don't set up twice
    if (this.#controlsObserver) return;

    this.#controlsRef = this.renderRoot.querySelector('.program-controls') as HTMLElement;
    this.#programContainerRef = this.renderRoot.querySelector('.program-container') as HTMLElement;

    if (this.#controlsRef) {
      this.#controlsObserver = new IntersectionObserver(
        (entries) => {
          // Show sticky bar when controls are not visible
          this._showStickyBar = !entries[0].isIntersecting;
        },
        { threshold: 0, rootMargin: '-1px 0px 0px 0px' }
      );
      this.#controlsObserver.observe(this.#controlsRef);
    }

    // Observe when program container goes out of view (scrolled past)
    if (this.#programContainerRef) {
      this.#programEndObserver = new IntersectionObserver(
        (entries) => {
          this._isProgramInView = entries[0].isIntersecting;
        },
        { threshold: 0 }
      );
      this.#programEndObserver.observe(this.#programContainerRef);
    }
  }

  protected willUpdate(changedProperties: PropertyValues): void {
    // Recompute filtered sessions when schedule, day, or filters change
    if (
      changedProperties.has("_schedule") ||
      changedProperties.has("_selectedDayIndex") ||
      changedProperties.has("_selectedFilters")
    ) {
      this.#updateFilteredSessions();
    }
  }

  protected updated(changedProperties: PropertyValues): void {
    // Set up observer after loading completes and controls are rendered
    if (changedProperties.has("_loading") && !this._loading) {
      // Wait for next frame to ensure DOM is fully rendered
      requestAnimationFrame(() => this.#setupControlsObserver());
    }
  }

  #updateFilteredSessions(): void {
    const selectedDay = this._schedule[this._selectedDayIndex];
    if (!selectedDay) {
      this.#filteredSessionsByRoom.clear();
      this.#filteredSessionIds.clear();
      this.#activeRooms = [];
      return;
    }

    // Build map of filtered sessions by room ID and set of filtered session IDs
    const newMap = new Map<number, SessionizeSession[]>();
    const newIds = new Set<string>();

    for (const room of selectedDay.rooms) {
      const sessions: SessionizeSession[] = [];
      for (const slot of selectedDay.timeSlots) {
        const slotRoom = slot.rooms.find((r) => r.id === room.id);
        if (slotRoom?.session && this.#sessionMatchesFilter(slotRoom.session)) {
          sessions.push(slotRoom.session);
          newIds.add(slotRoom.session.id);
        }
      }
      newMap.set(room.id, sessions);
    }

    this.#filteredSessionsByRoom = newMap;
    this.#filteredSessionIds = newIds;

    // Build list of rooms that have at least one matching session
    this.#activeRooms = selectedDay.rooms.filter(
      (room) => (newMap.get(room.id)?.length ?? 0) > 0
    );
  }

  async #loadSchedule() {
    try {
      this._loading = true;
      this._error = null;

      // Fetch schedule and categories in parallel
      const [schedule, categories] = await Promise.all([
        SessionizeService.getSchedule(),
        SessionizeService.getCategories(),
      ]);

      this._schedule = schedule;
      this._categories = categories;
      this._categoryDropdowns = this.#buildCategoryDropdowns(categories, schedule);

      // Default to today if it's a conference day, otherwise Wednesday, then Sessionize default, then first day
      const today = new Date();
      const todayDateStr = today.toISOString().split("T")[0]; // YYYY-MM-DD

      const todayIndex = schedule.findIndex((day) => {
        const dayDateStr = day.date.split("T")[0];
        return dayDateStr === todayDateStr;
      });

      if (todayIndex >= 0) {
        this._selectedDayIndex = todayIndex;
      } else {
        const wednesdayIndex = schedule.findIndex((day) => {
          const date = new Date(day.date);
          return date.getDay() === 3; // 3 = Wednesday
        });
        if (wednesdayIndex >= 0) {
          this._selectedDayIndex = wednesdayIndex;
        } else {
          const defaultIndex = schedule.findIndex((day) => day.isDefault);
          if (defaultIndex >= 0) {
            this._selectedDayIndex = defaultIndex;
          }
        }
      }
    } catch (error) {
      this._error =
        error instanceof Error ? error.message : "Failed to load program";
      console.error("Error loading program:", error);
    } finally {
      this._loading = false;
    }
  }

  #buildCategoryDropdowns(categories: SessionizeCategory[], schedule: SessionizeSchedule[]): CategoryDropdown[] {
    const dropdowns: CategoryDropdown[] = [];

    // Collect all category item IDs that are actually used in sessions
    const usedCategoryItemIds = new Set<number>();
    for (const day of schedule) {
      for (const slot of day.timeSlots) {
        for (const room of slot.rooms) {
          if (room.session?.categoryItems) {
            for (const itemId of room.session.categoryItems) {
              usedCategoryItemIds.add(itemId);
            }
          }
        }
      }
    }

    // Rename certain categories for clarity
    const titleMapping: Record<string, string> = {
      tags: "Topic",
      track: "Track",
      level: "Level",
    };

    // Include categories that make sense for filtering (Tags, Track, Level, etc.)
    // Exclude session format categories (those with names like "45 (regular talks)")
    for (const category of categories) {
      const isSessionFormat = category.items.some(
        (item) => item.name.toLowerCase().includes("regular talk") ||
                  item.name.toLowerCase().includes("minute")
      );

      if (!isSessionFormat && category.items.length > 0) {
        const lowerTitle = category.title.toLowerCase();
        const displayTitle = titleMapping[lowerTitle] || category.title;

        // Only include items that are actually used in sessions
        const usedItems = category.items.filter((item) => usedCategoryItemIds.has(item.id));

        if (usedItems.length > 0) {
          dropdowns.push({
            id: category.id,
            title: displayTitle,
            items: usedItems.map((item) => ({
              id: item.id,
              name: item.name,
            })),
          });
        }
      }
    }

    return dropdowns;
  }

  #addFilter(categoryId: number, itemId: number, itemName: string, categoryTitle: string) {
    // Don't add if already selected
    if (this._selectedFilters.some((f) => f.id === itemId)) {
      return;
    }

    this._selectedFilters = [
      ...this._selectedFilters,
      { id: itemId, name: itemName, categoryTitle },
    ];
  }

  #removeFilter(filterId: number) {
    this._selectedFilters = this._selectedFilters.filter((f) => f.id !== filterId);
  }

  #clearAllFilters() {
    this._selectedFilters = [];
  }

  #sessionMatchesFilter(session: SessionizeSession): boolean {
    // No filters selected = show everything
    if (this._selectedFilters.length === 0) {
      return true;
    }

    // OR logic: session matches if it has ANY of the selected category items
    return this._selectedFilters.some((filter) =>
      session.categoryItems.includes(filter.id)
    );
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

  #parseDateTime(timeString: string, dateString?: string): Date | null {
    try {
      let date: Date;
      let dateTimeStr: string;

      // Check if timeString is already a full ISO date or just a time
      if (timeString.includes("T") || timeString.includes("-")) {
        // Full ISO datetime string - ensure it's treated as UTC
        dateTimeStr = timeString;
      } else if (dateString) {
        // Just a time string (e.g., "09:00:00"), combine with the day's date
        // Extract just the date part (YYYY-MM-DD) in case dateString has a time component
        const datePart = dateString.split("T")[0];
        dateTimeStr = `${datePart}T${timeString}`;
      } else {
        // Fallback: try to parse as-is
        dateTimeStr = timeString;
      }

      // Ensure UTC interpretation by adding Z if no timezone indicator present
      if (!dateTimeStr.endsWith("Z") && !dateTimeStr.includes("+") && !dateTimeStr.includes("-", 10)) {
        dateTimeStr += "Z";
      }

      date = new Date(dateTimeStr);

      if (isNaN(date.getTime())) {
        return null;
      }

      return date;
    } catch {
      return null;
    }
  }

  #getDayOffset(date: Date, originalDateString: string): number {
    // Get the date in the selected timezone
    const localDateStr = date.toLocaleDateString("en-CA", {
      timeZone: this._selectedTimezone,
    }); // YYYY-MM-DD format

    // Get the original UTC date (just the date part)
    const originalDate = originalDateString.split("T")[0];

    // Compare dates
    const localDate = new Date(localDateStr);
    const origDate = new Date(originalDate);

    // Calculate difference in days
    const diffTime = localDate.getTime() - origDate.getTime();
    const diffDays = Math.round(diffTime / (1000 * 60 * 60 * 24));

    return diffDays;
  }

  #getTimezoneOffsetFromCopenhagen(): number {
    // Get current time to calculate timezone offsets
    const now = new Date();

    // Get time strings in both timezones
    const copenhagenTime = now.toLocaleString("en-US", { timeZone: DEFAULT_TIMEZONE, hour12: false });
    const selectedTime = now.toLocaleString("en-US", { timeZone: this._selectedTimezone, hour12: false });

    // Parse to get hours
    const copenhagenDate = new Date(copenhagenTime);
    const selectedDate = new Date(selectedTime);

    // Calculate difference in hours
    const diffMs = selectedDate.getTime() - copenhagenDate.getTime();
    const diffHours = diffMs / (1000 * 60 * 60);

    return diffHours;
  }

  #isDistantTimezone(): boolean {
    const offset = this.#getTimezoneOffsetFromCopenhagen();
    return Math.abs(offset) > 3;
  }

  #formatTime(timeString: string, dateString?: string): string {
    const date = this.#parseDateTime(timeString, dateString);

    if (!date) {
      // If invalid, just return the time portion
      return timeString.substring(0, 5); // "09:00:00" -> "09:00"
    }

    return date.toLocaleTimeString("en-GB", {
      hour: "2-digit",
      minute: "2-digit",
      timeZone: this._selectedTimezone,
    });
  }

  #formatTimeWithDayOffset(
    timeString: string,
    dateString: string
  ): { time: string; dayOffset: number } {
    const date = this.#parseDateTime(timeString, dateString);

    if (!date) {
      return { time: timeString.substring(0, 5), dayOffset: 0 };
    }

    const time = date.toLocaleTimeString("en-GB", {
      hour: "2-digit",
      minute: "2-digit",
      timeZone: this._selectedTimezone,
    });

    const dayOffset = this.#getDayOffset(date, dateString);

    return { time, dayOffset };
  }

  #onTimezoneChange(e: Event) {
    const select = e.target as HTMLSelectElement;
    this._selectedTimezone = select.value;
  }

  #renderTimezoneSelector() {
    return html`
      <div class="timezone-selector">
        <label for="timezone-select">Timezone:</label>
        <select
          id="timezone-select"
          @change=${(e: Event) => {
            const select = e.target as HTMLSelectElement;
            this._selectedTimezone = select.value;
          }}
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

  #onDropdownChange(e: Event, dropdown: CategoryDropdown) {
    const select = e.target as HTMLSelectElement;
    const itemId = parseInt(select.value, 10);

    if (isNaN(itemId)) return;

    const item = dropdown.items.find((i) => i.id === itemId);
    if (item) {
      this.#addFilter(dropdown.id, item.id, item.name, dropdown.title);
    }

    // Reset dropdown to placeholder
    select.value = "";
  }

  #hasActiveFiltersOrCustomTimezone(): boolean {
    return this._selectedFilters.length > 0 || this._selectedTimezone !== DEFAULT_TIMEZONE;
  }

  #getSelectedTimezoneLabel(): string {
    const option = TIMEZONE_OPTIONS.find(tz => tz.value === this._selectedTimezone);
    return option?.label || this._selectedTimezone;
  }

  #renderStickyBar() {
    // Only show if:
    // - Scrolled past controls AND
    // - Program is still in view AND
    // - Has active filters or custom timezone
    if (!this._showStickyBar || !this._isProgramInView || !this.#hasActiveFiltersOrCustomTimezone()) {
      return nothing;
    }

    return html`
      <div class="sticky-filter-bar">
        <div class="sticky-bar-content">
          ${when(
            this._selectedTimezone !== DEFAULT_TIMEZONE,
            () => html`
              <span class="sticky-timezone">
                <span class="sticky-label">Timezone:</span>
                ${this.#getSelectedTimezoneLabel()}
              </span>
            `
          )}
          ${when(
            this._selectedFilters.length > 0,
            () => html`
              <div class="sticky-filters">
                <span class="sticky-label">Filters:</span>
                ${this._selectedFilters.map(
                  (filter) => html`
                    <span class="filter-pill filter-pill-small">
                      ${filter.name}
                      <button
                        class="filter-pill-remove"
                        @click=${() => this.#removeFilter(filter.id)}
                        aria-label="Remove ${filter.name} filter"
                      >
                        &times;
                      </button>
                    </span>
                  `
                )}
                <button class="clear-filters-btn clear-filters-btn-small" @click=${() => this.#clearAllFilters()}>
                  Clear all
                </button>
              </div>
            `
          )}
        </div>
      </div>
    `;
  }

  #renderFilters() {
    if (this._categoryDropdowns.length === 0) return nothing;

    return html`
      <div class="session-filters">
        <div class="filter-controls">
          <span class="filter-label">Filter by:</span>
          <div class="filter-dropdowns">
            ${this._categoryDropdowns.map(
              (dropdown) => html`
                <select
                  class="filter-dropdown"
                  @change=${(e: Event) => this.#onDropdownChange(e, dropdown)}
                  aria-label="Filter by ${dropdown.title}"
                >
                  <option value="">Select ${dropdown.title}...</option>
                  ${dropdown.items
                    .filter((item) => !this._selectedFilters.some((f) => f.id === item.id))
                    .map(
                      (item) => html`
                        <option value=${item.id}>${item.name}</option>
                      `
                    )}
                </select>
              `
            )}
          </div>
        </div>
        ${when(
          this._selectedFilters.length > 0,
          () => html`
            <div class="selected-filters">
              <span class="active-filters-label">Active filters:</span>
              ${this._selectedFilters.map(
                (filter) => html`
                  <span class="filter-pill">
                    ${filter.name}
                    <button
                      class="filter-pill-remove"
                      @click=${() => this.#removeFilter(filter.id)}
                      aria-label="Remove ${filter.name} filter"
                    >
                      &times;
                    </button>
                  </span>
                `
              )}
              <button class="clear-filters-btn" @click=${() => this.#clearAllFilters()}>
                Clear all
              </button>
            </div>
          `
        )}
      </div>
    `;
  }

  #openSessionDialog(session: SessionizeSession) {
    const dialog = new SessionizeSessionDialogElement();
    dialog.session = session;
    dialog.timezone = this._selectedTimezone;
    dialog.categories = this._categories;
    dialog.schedule = this._schedule;
    dialog.addEventListener("filter-select", ((e: CustomEvent) => {
      const { id, name } = e.detail;
      this.#addFilterFromHashtag(id, name);
    }) as EventListener);
    this.#dialogHandler.open(dialog);
  }

  #addFilterFromHashtag(itemId: number, itemName: string) {
    // Don't add if already selected
    if (this._selectedFilters.some((f) => f.id === itemId)) {
      return;
    }

    // Find the category title for this item
    let categoryTitle = "";
    for (const category of this._categories) {
      if (category.items.some((i) => i.id === itemId)) {
        categoryTitle = category.title;
        break;
      }
    }

    this._selectedFilters = [
      ...this._selectedFilters,
      { id: itemId, name: itemName, categoryTitle },
    ];
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

  #renderDayOffsetBadge(dayOffset: number) {
    if (dayOffset === 0) return nothing;

    const label = dayOffset > 0 ? `+${dayOffset}` : `${dayOffset}`;
    const title =
      dayOffset > 0
        ? `${dayOffset} day${dayOffset > 1 ? "s" : ""} later in selected timezone`
        : `${Math.abs(dayOffset)} day${Math.abs(dayOffset) > 1 ? "s" : ""} earlier in selected timezone`;

    return html`<span class="day-offset-badge" title=${title}>${label}</span>`;
  }

  #getAllSessionsForDay(day: SessionizeSchedule): SessionizeSession[] {
    const sessions: SessionizeSession[] = [];
    for (const slot of day.timeSlots) {
      for (const room of slot.rooms) {
        if (room.session) {
          sessions.push(room.session);
        }
      }
    }
    return sessions;
  }

  #getSessionsForRoom(roomId: number): SessionizeSession[] {
    return this.#filteredSessionsByRoom.get(roomId) ?? [];
  }

  #getRoomsWithSessions(): SessionizeRoom[] {
    return this.#activeRooms;
  }

  #getTimeRange(day: SessionizeSchedule): { startHour: number; endHour: number } {
    const sessions = this.#getAllSessionsForDay(day);
    if (sessions.length === 0) {
      return { startHour: 8, endHour: 18 };
    }

    let minTime = Infinity;
    let maxTime = -Infinity;

    for (const session of sessions) {
      if (session.startsAt) {
        const date = this.#parseDateTime(session.startsAt);
        if (date) {
          const hours = this.#getHoursInTimezone(date);
          minTime = Math.min(minTime, hours);
        }
      }
      if (session.endsAt) {
        const date = this.#parseDateTime(session.endsAt);
        if (date) {
          const hours = this.#getHoursInTimezone(date);
          maxTime = Math.max(maxTime, hours);
        }
      }
    }

    // Round to full hours with padding
    const startHour = Math.floor(minTime);
    const endHour = Math.ceil(maxTime);

    return { startHour, endHour };
  }

  #getHoursInTimezone(date: Date): number {
    const timeStr = date.toLocaleTimeString("en-GB", {
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
      timeZone: this._selectedTimezone,
    });
    const [hours, minutes] = timeStr.split(":").map(Number);
    return hours + minutes / 60;
  }

  #getSessionPositionVariable(
    session: SessionizeSession,
    startHour: number,
    hours: { hour: number; height: number }[],
    totalHeight: number
  ): { top: number; height: number } | null {
    if (!session.startsAt || !session.endsAt) {
      return null;
    }

    const startDate = this.#parseDateTime(session.startsAt);
    const endDate = this.#parseDateTime(session.endsAt);

    if (!startDate || !endDate) {
      return null;
    }

    const sessionStartHours = this.#getHoursInTimezone(startDate);
    const sessionEndHours = this.#getHoursInTimezone(endDate);

    // Calculate pixel position by summing up heights for each hour
    let topPixels = 0;
    let heightPixels = 0;

    for (const { hour, height } of hours) {
      const hourEnd = hour + 1;

      // Calculate top position (before session starts)
      if (hourEnd <= sessionStartHours) {
        topPixels += height;
      } else if (hour < sessionStartHours) {
        // Partial hour at the start
        topPixels += height * (sessionStartHours - hour);
      }

      // Calculate height (session duration)
      if (hour >= sessionStartHours && hourEnd <= sessionEndHours) {
        // Full hour within session
        heightPixels += height;
      } else if (hour < sessionStartHours && hourEnd > sessionStartHours && hourEnd <= sessionEndHours) {
        // Session starts mid-hour
        heightPixels += height * (hourEnd - sessionStartHours);
      } else if (hour >= sessionStartHours && hour < sessionEndHours && hourEnd > sessionEndHours) {
        // Session ends mid-hour
        heightPixels += height * (sessionEndHours - hour);
      } else if (hour < sessionStartHours && hourEnd > sessionEndHours) {
        // Session entirely within this hour
        heightPixels += height * (sessionEndHours - sessionStartHours);
      }
    }

    const top = (topPixels / totalHeight) * 100;
    const heightPercent = (heightPixels / totalHeight) * 100;

    return { top, height: heightPercent };
  }

  #formatSessionTime(dateString: string): string {
    const date = this.#parseDateTime(dateString);
    if (!date) return "";

    return date.toLocaleTimeString("en-GB", {
      hour: "2-digit",
      minute: "2-digit",
      timeZone: this._selectedTimezone,
    });
  }

  #renderScheduleGrid() {
    const selectedDay = this._schedule[this._selectedDayIndex];
    if (!selectedDay) {
      return html`<div class="empty-state">No schedule available.</div>`;
    }

    // Only show rooms that have sessions matching current filters (pre-computed in willUpdate)
    const rooms = this.#getRoomsWithSessions();
    if (rooms.length === 0) {
      return html`<div class="empty-state">No sessions match the current filters.</div>`;
    }

    const { startHour, endHour } = this.#getTimeRange(selectedDay);
    const isDistant = this.#isDistantTimezone();

    // Use uniform height for distant timezones, variable for local
    const hourHeight = 200; // pixels per hour
    const earlyHourHeight = isDistant ? hourHeight : 90; // half-height for hours before 9am (Copenhagen time only)
    const earlyHourCutoff = 9;

    // Generate hour markers with their heights
    const hours: { hour: number; height: number }[] = [];
    let totalHeight = 0;

    for (let h = startHour; h <= endHour; h++) {
      const height = (h < earlyHourCutoff && !isDistant) ? earlyHourHeight : hourHeight;
      hours.push({ hour: h, height });
      totalHeight += height;
    }

    return html`
      <div class="schedule-timeline">
        <div class="timeline-header">
          <div class="timeline-time-column"></div>
          <div class="timeline-rooms-header">
            ${rooms.map(
              (room) => html`<div class="timeline-room-header">${room.name}</div>`
            )}
          </div>
        </div>
        <div class="timeline-body" style="height: ${totalHeight}px;">
          <div class="timeline-time-column">
            ${hours.map(
              ({ hour, height }) => {
                const displayHour = hour >= 24 ? hour - 24 : (hour < 0 ? hour + 24 : hour);
                return html`
                  <div class="timeline-hour ${(hour < earlyHourCutoff && !isDistant) ? "early-hour" : ""}" style="height: ${height}px;">
                    <span class="timeline-hour-label">${displayHour.toString().padStart(2, "0")}:00</span>
                  </div>
                `;
              }
            )}
          </div>
          <div class="timeline-rooms">
            ${rooms.map((room) => this.#renderTimelineRoom(room, startHour, hours, totalHeight))}
          </div>
        </div>
      </div>
    `;
  }

  #renderTimelineRoom(
    room: SessionizeRoom,
    startHour: number,
    hours: { hour: number; height: number }[],
    totalHeight: number
  ) {
    // Sessions are pre-filtered in willUpdate
    const sessions = this.#getSessionsForRoom(room.id);

    return html`
      <div class="timeline-room-column">
        ${sessions.map((session) => {
          const position = this.#getSessionPositionVariable(session, startHour, hours, totalHeight);
          if (!position) return nothing;

          const speakerNames = session.speakers?.map((s) => s.fullName).join(", ") || "";
          const isBreakOrActivity = session.isServiceSession || session.categoryItems.length === 0;
          const startTime = session.startsAt ? this.#formatSessionTime(session.startsAt) : "";
          const endTime = session.endsAt ? this.#formatSessionTime(session.endsAt) : "";

          // Calculate duration in minutes for short break detection
          let durationMinutes = 0;
          if (session.startsAt && session.endsAt) {
            const start = this.#parseDateTime(session.startsAt);
            const end = this.#parseDateTime(session.endsAt);
            if (start && end) {
              durationMinutes = (end.getTime() - start.getTime()) / (1000 * 60);
            }
          }
          const isShortBreak = isBreakOrActivity && durationMinutes <= 15;

          return html`
            <div
              class="timeline-session ${isBreakOrActivity ? "service-session" : ""} ${isShortBreak ? "short-break" : ""}"
              style="top: ${position.top}%; height: ${position.height}%;"
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
              ${isShortBreak
                ? html`<span class="session-title-inline" title=${session.title}>${session.title}</span><span class="session-time-inline">${startTime} - ${endTime}</span>`
                : html`
                    <h4 class="session-title" title=${session.title}>${session.title}</h4>
                    ${when(
                      speakerNames,
                      () => html`<p class="session-speakers" title=${speakerNames}>${speakerNames}</p>`
                    )}
                    <p class="session-time">${startTime} - ${endTime}</p>
                  `
              }
            </div>
          `;
        })}
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
        ${selectedDay.timeSlots.map((slot) => {
          const { time, dayOffset } = this.#formatTimeWithDayOffset(slot.slotStart, selectedDay.date);
          return html`
            <div class="mobile-time-slot ${dayOffset !== 0 ? "different-day" : ""}">
              <div class="mobile-time">
                ${time}
                ${this.#renderDayOffsetBadge(dayOffset)}
              </div>
              <div class="mobile-sessions">
                ${slot.rooms
                  .filter((room) => room.session && this.#filteredSessionIds.has(room.session.id))
                  .map((room) => {
                    const isBreakOrActivity = room.session?.isServiceSession || room.session?.categoryItems.length === 0;
                    return html`
                      <div
                        class="mobile-session-card ${isBreakOrActivity ? "service-session" : ""}"
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
                        ${when(
                          room.session?.startsAt && room.session?.endsAt,
                          () => html`
                            <p class="session-time">
                              ${this.#formatSessionTime(room.session!.startsAt!)} - ${this.#formatSessionTime(room.session!.endsAt!)}
                            </p>
                          `
                        )}
                      </div>
                    `;
                  })}
              </div>
            </div>
          `;
        })}
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
      ${this.#renderStickyBar()}
      <div class="program-container">
        <div class="program-controls">
          ${this.#renderFilters()}
          ${this.#renderTimezoneSelector()}
        </div>
        ${this.#renderTabs()}
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

    /* Sticky filter bar - matches header's centering and sizing behavior */
    .sticky-filter-bar {
      position: fixed;
      top: 125px; /* Below the main header */
      left: 86px; /* Match header's 86px margin */
      right: 86px;
      margin-left: auto;
      margin-right: auto;
      max-width: 1360px; /* Match the actual header nav bar width */
      z-index: 50; /* Below header (z-index 2) but above content */
      background: linear-gradient(to right, var(--color-blue, #3544b1), var(--color-blue-dark, #2a3690));
      color: var(--color-white, #fff);
      box-shadow: 0 4px 12px rgba(53, 68, 177, 0.3);
      padding: var(--unit-xs, 0.5rem) var(--unit, 1rem);
      border-radius: var(--border-radius-xl);
      animation: slideDown 0.25s ease-out forwards;
    }

    @keyframes slideDown {
      from {
        transform: translateY(-20px);
        opacity: 0;
      }
      to {
        transform: translateY(0);
        opacity: 1;
      }
    }

    .sticky-bar-content {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--unit-sm, 0.75rem);
      max-width: 1400px;
      margin: 0 auto;
    }

    .sticky-bar-content::before {
      content: "Program";
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      opacity: 0.8;
      padding-right: var(--unit-xs, 0.5rem);
      border-right: 1px solid rgba(255, 255, 255, 0.3);
      margin-right: var(--unit-xs, 0.5rem);
    }

    .sticky-timezone {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      font-size: 0.85rem;
      color: var(--color-white, #fff);
      padding: 0.25rem 0.5rem;
      background: rgba(255, 255, 255, 0.15);
      border-radius: var(--border-radius, 6px);
    }

    .sticky-filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
    }

    .sticky-label {
      font-size: 0.8rem;
      font-weight: 600;
      color: rgba(255, 255, 255, 0.85);
    }

    .sticky-filter-bar .filter-pill {
      background: rgba(255, 255, 255, 0.2);
      color: var(--color-white, #fff);
    }

    .sticky-filter-bar .filter-pill-remove {
      background: rgba(255, 255, 255, 0.2);
    }

    .sticky-filter-bar .filter-pill-remove:hover {
      background: rgba(255, 255, 255, 0.4);
    }

    .filter-pill-small {
      padding: 0.25rem 0.4rem 0.25rem 0.6rem;
      font-size: 0.8rem;
    }

    .filter-pill-small .filter-pill-remove {
      width: 1.1rem;
      height: 1.1rem;
      font-size: 0.9rem;
    }

    .sticky-filter-bar .clear-filters-btn {
      background: transparent;
      border-color: rgba(255, 255, 255, 0.5);
      color: var(--color-white, #fff);
    }

    .sticky-filter-bar .clear-filters-btn:hover {
      background: rgba(255, 255, 255, 0.15);
      border-color: var(--color-white, #fff);
      color: var(--color-white, #fff);
    }

    .clear-filters-btn-small {
      padding: 0.25rem 0.5rem;
      font-size: 0.8rem;
    }

    /* --xl breakpoint (1408px+): match underlying content width */
    @media (min-width: 1408px) {
      .sticky-filter-bar {
        left: 0;
        right: 0;
        max-width: 1322px;
        margin-left: auto;
        margin-right: auto;
      }
    }

    /* --lg breakpoint (1216px - 1407px): match header's margin behavior */
    @media (min-width: 1216px) and (max-width: 1407px) {
      .sticky-filter-bar {
        left: 2rem;
        right: 2rem;
      }
    }

    /* --md breakpoint (1024px - 1215px): tablet screens */
    @media (min-width: 1024px) and (max-width: 1215px) {
      .sticky-filter-bar {
        top: 115px;
        left: 2rem;
        right: 2rem;
      }
    }

    /* --sm breakpoint (768px - 1023px): small tablets */
    @media (min-width: 768px) and (max-width: 1023px) {
      .sticky-filter-bar {
        top: 65px;
        left: 2rem;
        right: 2rem;
      }
    }

    /* Below --sm (< 768px): mobile screens */
    @media (max-width: 767px) {
      .sticky-filter-bar {
        top: 80px;
        left: 1rem;
        right: 1rem;
        padding: var(--unit-xs, 0.5rem) var(--unit, 1rem);
      }

      .sticky-bar-content {
        gap: var(--unit-xs, 0.5rem);
      }

      .sticky-bar-content::before {
        font-size: 0.65rem;
        padding-right: 0.35rem;
        margin-right: 0.35rem;
      }

      .sticky-timezone {
        font-size: 0.75rem;
        padding: 0.2rem 0.4rem;
      }

      .sticky-label {
        font-size: 0.7rem;
      }

      .filter-pill-small {
        padding: 0.2rem 0.35rem 0.2rem 0.5rem;
        font-size: 0.75rem;
      }

      .filter-pill-small .filter-pill-remove {
        width: 1rem;
        height: 1rem;
        font-size: 0.8rem;
      }

      .clear-filters-btn-small {
        padding: 0.2rem 0.4rem;
        font-size: 0.75rem;
      }
    }

    /* Controls container (filters + timezone) */
    .program-controls {
      display: flex;
      flex-direction: column;
      gap: var(--unit, 1rem);
      margin-bottom: var(--unit, 1rem);
    }

    @media (min-width: 768px) {
      .program-controls {
        flex-direction: row;
        align-items: center;
        justify-content: space-between;
        gap: var(--unit-md, 1.5rem);
      }
    }

    /* Session filters */
    .session-filters {
      display: flex;
      flex-direction: column;
      gap: var(--unit-sm, 0.75rem);
    }

    .filter-controls {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
    }

    .filter-label {
      font-size: 0.9rem;
      font-weight: 500;
      color: var(--color-dark, #1b264f);
      white-space: nowrap;
    }

    .filter-dropdowns {
      display: flex;
      flex-wrap: wrap;
      gap: var(--unit-xs, 0.5rem);
    }

    .filter-dropdown {
      padding: var(--unit-xs, 0.5rem) var(--unit-sm, 0.75rem);
      font-size: 0.9rem;
      border: 1px solid var(--color-grey, #d1d5db);
      border-radius: var(--border-radius, 6px);
      background: var(--color-white, #fff);
      color: var(--color-dark, #1b264f);
      cursor: pointer;
      min-width: 140px;
      transition: border-color 0.2s ease, box-shadow 0.2s ease;
    }

    .filter-dropdown:hover {
      border-color: var(--color-blue, #3544b1);
    }

    .filter-dropdown:focus {
      outline: none;
      border-color: var(--color-blue, #3544b1);
      box-shadow: 0 0 0 3px rgba(53, 68, 177, 0.15);
    }

    .selected-filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
    }

    .active-filters-label {
      font-size: 0.85rem;
      font-weight: 500;
      color: var(--color-grey-dark, #6b7280);
      white-space: nowrap;
    }

    .filter-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.35rem 0.5rem 0.35rem 0.75rem;
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
      border-radius: var(--border-radius-lg, 20px);
      font-size: 0.85rem;
      font-weight: 500;
    }

    .filter-pill-remove {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1.25rem;
      height: 1.25rem;
      padding: 0;
      background: rgba(255, 255, 255, 0.2);
      border: none;
      border-radius: 50%;
      color: var(--color-white, #fff);
      font-size: 1rem;
      line-height: 1;
      cursor: pointer;
      transition: background-color 0.2s ease;
    }

    .filter-pill-remove:hover {
      background: rgba(255, 255, 255, 0.35);
    }

    .clear-filters-btn {
      padding: 0.35rem 0.75rem;
      font-size: 0.85rem;
      font-weight: 500;
      border: 1px solid var(--color-grey, #d1d5db);
      border-radius: var(--border-radius, 6px);
      background: var(--color-white, #fff);
      color: var(--color-grey-dark, #6b7280);
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .clear-filters-btn:hover {
      border-color: var(--color-red, #dc2626);
      color: var(--color-red, #dc2626);
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
      flex-wrap: wrap;
      justify-content: center;
      gap: var(--unit-xs, 0.5rem);
      padding-bottom: var(--unit-xs, 0.5rem);
      margin-bottom: var(--unit-md, 1.5rem);
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

    /* Timeline Layout */
    .schedule-timeline {
      border: 1px solid var(--color-grey-light, #e5e7eb);
      border-radius: var(--border-radius-lg, 8px);
      overflow: hidden;
      background: var(--color-white, #fff);
    }

    .timeline-header {
      display: flex;
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
      position: sticky;
      top: 0;
      z-index: 10;
    }

    .timeline-time-column {
      width: 70px;
      flex-shrink: 0;
      box-sizing: border-box;
    }

    .timeline-header .timeline-time-column {
      border-right: 1px solid rgba(255, 255, 255, 0.2);
    }

    .timeline-body .timeline-time-column {
      background: var(--color-grey-light, #f9f9f9);
      border-right: 1px solid var(--color-grey-light, #e5e7eb);
    }

    .timeline-rooms-header {
      display: flex;
      flex: 1;
    }

    .timeline-room-header {
      flex: 1;
      padding: var(--unit, 1rem);
      font-weight: 600;
      text-align: center;
      border-right: 1px solid rgba(255, 255, 255, 0.2);
      min-width: 150px;
    }

    .timeline-room-header:last-child {
      border-right: none;
    }

    .timeline-body {
      display: flex;
      position: relative;
    }

    .timeline-hour {
      display: flex;
      align-items: flex-start;
      justify-content: center;
      padding-top: 0.25rem;
      border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
      box-sizing: border-box;
    }

    .timeline-hour:last-child {
      border-bottom: none;
    }

    .timeline-hour-label {
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--color-dark, #1b264f);
    }

    .timeline-rooms {
      display: flex;
      flex: 1;
      position: relative;
    }

    .timeline-room-column {
      flex: 1;
      position: relative;
      border-right: 1px solid var(--color-grey-light, #e5e7eb);
      min-width: 150px;
    }

    .timeline-room-column:last-child {
      border-right: none;
    }

    .timeline-session {
      position: absolute;
      left: 4px;
      right: 4px;
      padding: 0.5rem;
      background: var(--color-white, #fff);
      border: 1px solid var(--color-blue, #3544b1);
      border-left: 4px solid var(--color-blue, #3544b1);
      border-radius: var(--border-radius, 6px);
      cursor: pointer;
      overflow: hidden;
      transition: box-shadow 0.2s ease, transform 0.1s ease;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
    }

    .timeline-session:hover {
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      z-index: 5;
    }

    .timeline-session.service-session {
      background: var(--color-grey-light, #f9f9f9);
      border-color: var(--color-grey, #d1d5db);
      border-left-color: var(--color-grey, #d1d5db);
    }

    .timeline-session.short-break {
      flex-direction: row;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      text-align: center;
    }

    .session-title-inline {
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--color-dark, #1b264f);
      white-space: nowrap;
    }

    .session-time-inline {
      font-size: 0.75rem;
      font-weight: 500;
      color: var(--color-grey-dark, #6b7280);
      white-space: nowrap;
    }

    .timeline-session .session-title {
      margin: 0 0 0.25rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--color-dark, #1b264f);
      line-height: 1.2;
      /* Allow title to grow and shrink based on available space */
      flex: 1 1 auto;
      overflow: hidden;
      /* Fade out overflow text */
      mask-image: linear-gradient(to bottom, black calc(100% - 0.5rem), transparent 100%);
      -webkit-mask-image: linear-gradient(to bottom, black calc(100% - 0.5rem), transparent 100%);
    }

    .timeline-session .session-speakers {
      margin: 0 0 0.25rem;
      font-size: 0.75rem;
      color: var(--color-blue, #3544b1);
      flex-shrink: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .timeline-session .session-time {
      margin: 0;
      margin-top: auto;
      font-size: 0.7rem;
      font-weight: 500;
      color: var(--color-grey-dark, #6b7280);
    }

    .day-offset-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.1rem 0.4rem;
      background: var(--color-orange, #f59e0b);
      color: var(--color-white, #fff);
      border-radius: var(--border-radius, 4px);
      font-size: 0.7rem;
      font-weight: 700;
      line-height: 1;
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

    .mobile-time-slot.different-day {
      border-left-color: var(--color-orange, #f59e0b);
    }

    .mobile-time {
      display: flex;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
      font-size: 1.1rem;
      font-weight: 700;
      color: var(--color-blue, #3544b1);
      margin-bottom: var(--unit-sm, 0.75rem);
    }

    .mobile-time-slot.different-day .mobile-time {
      color: var(--color-orange, #f59e0b);
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

    .mobile-session-card .session-time {
      margin: var(--unit-xs, 0.5rem) 0 0;
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--color-grey-dark, #6b7280);
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

    /* Mobile styles */
    @media (max-width: 600px) {
      .program-container {
        margin-top: var(--unit, 1rem);
      }

      .program-controls {
        gap: var(--unit-sm, 0.75rem);
        margin-bottom: var(--unit-sm, 0.75rem);
      }

      .filter-label,
      .active-filters-label {
        font-size: 0.8rem;
      }

      .filter-dropdown {
        padding: var(--unit-xs, 0.5rem);
        font-size: 0.85rem;
        min-width: 120px;
      }

      .filter-pill {
        padding: 0.25rem 0.4rem 0.25rem 0.6rem;
        font-size: 0.8rem;
      }

      .timezone-selector label {
        font-size: 0.8rem;
      }

      .timezone-selector select {
        padding: var(--unit-xs, 0.5rem);
        font-size: 0.85rem;
        min-width: 150px;
      }

      /* Day tabs - stack vertically on mobile */
      .program-tabs {
        flex-direction: column;
        gap: 0.35rem;
        margin-bottom: var(--unit, 1rem);
        padding: 0.25rem;
        background: var(--color-grey-light, #f5f5f5);
        border-radius: var(--border-radius-lg, 8px);
      }

      .program-tab {
        width: 100%;
        padding: var(--unit-sm, 0.75rem);
        font-size: 0.9rem;
        text-align: center;
        border-radius: var(--border-radius, 6px);
      }

      /* Filter controls - stack on mobile */
      .filter-controls {
        flex-direction: column;
        align-items: stretch;
      }

      .filter-dropdowns {
        flex-direction: column;
      }

      .filter-dropdown {
        width: 100%;
        min-width: unset;
      }

      .selected-filters {
        flex-direction: column;
        align-items: stretch;
      }

      .filter-pill {
        justify-content: space-between;
      }

      .clear-filters-btn {
        align-self: flex-start;
      }

      /* Mobile schedule */
      .mobile-time-slot {
        padding-left: var(--unit-sm, 0.75rem);
      }

      .mobile-time {
        font-size: 1rem;
        margin-bottom: var(--unit-xs, 0.5rem);
      }

      .mobile-sessions {
        gap: var(--unit-xs, 0.5rem);
      }

      .mobile-session-card {
        padding: var(--unit-sm, 0.75rem);
      }

      .mobile-room-badge {
        font-size: 0.7rem;
        padding: 0.15rem 0.4rem;
        margin-bottom: 0.35rem;
      }

      .mobile-session-card .session-title {
        font-size: 0.9rem;
        margin-bottom: 0.25rem;
      }

      .mobile-session-card .session-speakers {
        font-size: 0.8rem;
        margin-bottom: 0.25rem;
      }

      .mobile-session-card .session-time {
        font-size: 0.75rem;
        margin-top: 0.25rem;
      }

      .loading,
      .error,
      .empty-state {
        padding: var(--unit-md, 1.5rem);
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: SessionizeProgramElement;
  }
}
