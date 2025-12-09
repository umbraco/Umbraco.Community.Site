import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { z } from 'zod';

// Validation schemas
const SearchInputSchema = z.string().max(200, 'Search query too long');
const DateInputSchema = z.string().regex(/^\d{4}-\d{2}-\d{2}$|^$/, 'Invalid date format');

export interface FilterChangeEvent {
  searchQuery: string;
  startDate: string;
  endDate: string;
  showInPerson: boolean;
  showOnline: boolean;
  organizationType: 'all' | 'community' | 'umbraco';
}

@customElement('events-calendar-filters')
export class EventsCalendarFilters extends LitElement {
  @property({ type: String }) searchQuery = '';
  @property({ type: String }) startDate = '';
  @property({ type: String }) endDate = '';
  @property({ type: Boolean }) showInPerson = false;
  @property({ type: Boolean }) showOnline = false;
  @property({ type: String }) organizationType: 'all' | 'community' | 'umbraco' = 'all';

  @state() private searchError = '';
  @state() private startDateError = '';
  @state() private endDateError = '';

  private debounceTimer: number | null = null;
  private readonly debounceDelay = 300; // milliseconds

  disconnectedCallback() {
    super.disconnectedCallback();
    // Clean up any pending debounce timer
    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
  }

  static styles = css`
    :host {
      display: block;
      margin-bottom: var(--unit-md);
    }

    .filters-container {
      display: flex;
      gap: var(--unit-sm);
      align-items: center;
      flex-wrap: wrap;
    }

    .search-input {
      flex: 1;
      min-width: 15.625rem;
    }

    .date-input {
      width: 8.75rem;
    }

    .type-select {
      width: 10rem;
    }

    .input-wrapper {
      display: flex;
      flex-direction: column;
      gap: var(--unit-xxs);
      position: relative;
    }

    .date-input-placeholder {
      position: absolute;
      inset: 0;
      display: none;
      align-items: center;
      padding: 0 var(--unit-sm);
      color: var(--color-dark-grey);
      font-size: 0.938rem;
      pointer-events: none;
    }

    input[type="date"].input:not(:focus):invalid {
      color: var(--color-dark-grey);
    }

    input[type="date"].input::-webkit-datetime-edit-text,
    input[type="date"].input::-webkit-datetime-edit-month-field,
    input[type="date"].input::-webkit-datetime-edit-day-field,
    input[type="date"].input::-webkit-datetime-edit-year-field {
      color: var(--color-dark-grey);
    }

    .input {
      box-sizing: border-box;
      border: 0.0625rem solid white;
      border-radius: 0.5rem;
      padding: var(--unit-xs) var(--unit-sm);
      font-size: 0.938rem;
      font-family: var(--font-family);
      background-color: white;
      color: var(--color-identity-darkest);
      transition: border-color 0.2s ease;
    }

    .input:focus {
      outline: none;
      border-color: var(--color-identity-blue);
      box-shadow: 0 0 0 0.125rem rgba(0, 123, 255, 0.25);
    }

    .date-input .input {
      height: 2.25rem;
      min-height: 2.25rem;
    }

    .input::placeholder {
      color: var(--color-dark-grey);
    }

    .checkbox-group {
      display: flex;
      flex-direction: column;
      gap: 0;
      align-items: flex-start;
    }

    .checkbox-option {
      display: flex;
      align-items: center;
      gap: var(--unit-xxs);
      white-space: nowrap;
    }

    .type-dropdown {
      cursor: pointer;
      padding-right: calc(var(--unit-sm) + 1.75rem);
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='8' viewBox='0 0 12 8'%3E%3Cpath fill='%23666666' d='M1.41.59 6 5.17 10.59.59 12 2 6 8 0 2z'/%3E%3C/svg%3E");
      background-repeat: no-repeat;
      background-position: right calc(var(--unit-xs) + 0.4rem) center;
      background-size: 0.75rem;
      appearance: none;
      -webkit-appearance: none;
      -moz-appearance: none;
      text-overflow: ellipsis;
      overflow: hidden;
      white-space: nowrap;
      color: var(--color-dark-grey);
    }

    .type-dropdown:hover {
      border-color: #283a97;
    }

    .type-dropdown:focus {
      outline: none;
      border-color: var(--color-identity-blue);
      box-shadow: 0 0 0 0.125rem rgba(0, 123, 255, 0.25);
    }

    /* Style dropdown options */
    .type-dropdown option {
      background-color: var(--color-identity-white);
      color: var(--color-identity-darkest);
      padding: var(--unit-xs);
    }

    .checkbox-input {
      margin: 0;
      cursor: pointer;
    }

    .checkbox-label {
      font-size: 0.938rem;
      color: var(--color-dark-grey);
      cursor: pointer;
      font-family: var(--font-family);
    }

    .clear-button {
      background-color: #283a97;
      color: var(--color-identity-white);
      border: none;
      border-radius: 0.5rem;
      padding: var(--unit-xs) var(--unit-sm);
      font-size: 0.938rem;
      font-weight: 500;
      font-family: var(--font-family);
      cursor: pointer;
      transition: background-color 0.2s ease;
      white-space: nowrap;
    }

    .clear-button:hover {
    }

    .clear-button:focus {
      outline: none;
      box-shadow: 0 0 0 0.125rem rgba(108, 117, 125, 0.5);
    }

    .error-message {
      color: #dc3545;
      font-size: var(--font-size-sm);
      margin-top: var(--unit-xxs);
    }

    @media (max-width: 768px) {
      .filters-container {
        flex-direction: column;
        align-items: stretch;
      }

      .search-input,
      .date-input,
      .type-select {
        width: 100%;
        min-width: unset;
        max-width: 100%;
      }

      .date-input .input {
        display: block;
        width: 100%;
        min-width: 100%;
        height: 2.25rem;
        min-height: 2.25rem;
        -webkit-appearance: none;
        appearance: none;
      }

      /* Ensure calendar picker icon is still visible */
      input[type="date"].input::-webkit-calendar-picker-indicator {
        opacity: 1;
        display: block;
        width: 1.25rem;
        height: 1.25rem;
        cursor: pointer;
      }

      .checkbox-group {
        justify-content: flex-start;
        width: 100%;
      }

      .date-input-placeholder {
        display: flex;
        opacity: 1;
      }

      .date-input .input:focus + .date-input-placeholder {
        opacity: 0;
      }

      /* Hide native date placeholder on mobile - only show custom overlay */
      input[type="date"].input::-webkit-datetime-edit-fields-wrapper {
        opacity: 0;
      }

      input[type="date"].input::-webkit-datetime-edit-text,
      input[type="date"].input::-webkit-datetime-edit-month-field,
      input[type="date"].input::-webkit-datetime-edit-day-field,
      input[type="date"].input::-webkit-datetime-edit-year-field {
        color: transparent;
      }

      /* Show the actual date value when one is selected */
      input[type="date"].input:focus::-webkit-datetime-edit-fields-wrapper,
      input[type="date"].input:not(:placeholder-shown)::-webkit-datetime-edit-fields-wrapper {
        opacity: 1;
      }

      input[type="date"].input:focus::-webkit-datetime-edit-text,
      input[type="date"].input:focus::-webkit-datetime-edit-month-field,
      input[type="date"].input:focus::-webkit-datetime-edit-day-field,
      input[type="date"].input:focus::-webkit-datetime-edit-year-field {
        color: var(--color-identity-darkest);
      }
    }
  `;

  private validateAndEmitChange() {
    // Validate inputs
    this.searchError = '';
    this.startDateError = '';
    this.endDateError = '';

    try {
      SearchInputSchema.parse(this.searchQuery);
    } catch (error) {
      if (error instanceof z.ZodError) {
        this.searchError = error.issues[0]?.message || 'Invalid search query';
        return;
      }
    }

    try {
      DateInputSchema.parse(this.startDate);
    } catch (error) {
      if (error instanceof z.ZodError) {
        this.startDateError = error.issues[0]?.message || 'Invalid start date';
        return;
      }
    }

    try {
      DateInputSchema.parse(this.endDate);
    } catch (error) {
      if (error instanceof z.ZodError) {
        this.endDateError = error.issues[0]?.message || 'Invalid end date';
        return;
      }
    }

    // Additional validation: start date should be before end date
    if (this.startDate && this.endDate && this.startDate > this.endDate) {
      this.endDateError = 'End date must be after start date';
      return;
    }

    // Emit change event if validation passes
    const event = new CustomEvent<FilterChangeEvent>('filter-change', {
      detail: {
        searchQuery: this.searchQuery,
        startDate: this.startDate,
        endDate: this.endDate,
        showInPerson: this.showInPerson,
        showOnline: this.showOnline,
        organizationType: this.organizationType,
      },
      bubbles: true,
    });
    this.dispatchEvent(event);
  }

  private handleSearchInput(e: Event) {
    const target = e.target as HTMLInputElement;
    this.searchQuery = target.value;
    this.debouncedValidateAndEmitChange();
  }

  private debouncedValidateAndEmitChange() {
    // Clear existing timer
    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
    }

    // Set new timer
    this.debounceTimer = window.setTimeout(() => {
      this.validateAndEmitChange();
      this.debounceTimer = null;
    }, this.debounceDelay);
  }

  private handleStartDateChange(e: Event) {
    const target = e.target as HTMLInputElement;
    this.startDate = target.value;
    this.validateAndEmitChange();
  }

  private handleEndDateChange(e: Event) {
    const target = e.target as HTMLInputElement;
    this.endDate = target.value;
    this.validateAndEmitChange();
  }

  private handleOrganizationSelect(event: Event) {
    const value = (event.target as HTMLSelectElement).value as 'all' | 'community' | 'umbraco';
    this.organizationType = value;
    this.debouncedValidateAndEmitChange();
  }

  private handleInPersonChange(e: Event) {
    const target = e.target as HTMLInputElement;
    this.showInPerson = target.checked;
    this.debouncedValidateAndEmitChange();
  }

  private handleOnlineChange(e: Event) {
    const target = e.target as HTMLInputElement;
    this.showOnline = target.checked;
    this.debouncedValidateAndEmitChange();
  }

  private handleClearFilters() {
    this.searchQuery = '';
    this.startDate = '';
    this.endDate = '';
    this.showInPerson = false;
    this.showOnline = false;
    this.organizationType = 'all';
    this.searchError = '';
    this.startDateError = '';
    this.endDateError = '';
    this.validateAndEmitChange();
  }

  render() {
    return html`
      <div class="filters-container">
        <div class="input-wrapper search-input">
          <input
            type="text"
            class="input"
            placeholder="Search events by name, description, or location"
            .value=${this.searchQuery}
            @input=${this.handleSearchInput}
            aria-label="Search events"
          />
          ${this.searchError ? html`<div class="error-message">${this.searchError}</div>` : ''}
        </div>

        <div class="input-wrapper date-input">
          <input
            id="start-date-input"
            type="date"
            class="input"
            .value=${this.startDate}
            @change=${this.handleStartDateChange}
            aria-label="Start date filter"
          />
          ${!this.startDate ? html`<span class="date-input-placeholder">Start date</span>` : ''}
          ${this.startDateError ? html`<div class="error-message">${this.startDateError}</div>` : ''}
        </div>

        <div class="input-wrapper date-input">
          <input
            id="end-date-input"
            type="date"
            class="input"
            .value=${this.endDate}
            @change=${this.handleEndDateChange}
            aria-label="End date filter"
          />
          ${!this.endDate ? html`<span class="date-input-placeholder">End date</span>` : ''}
          ${this.endDateError ? html`<div class="error-message">${this.endDateError}</div>` : ''}
        </div>

        <div class="input-wrapper type-select">
          <select
            id="type-filter"
            class="input type-dropdown"
            .value=${this.organizationType}
            @change=${this.handleOrganizationSelect}
            aria-label="Filter by type"
          >
            <option value="all">All</option>
            <option value="community">Community Event</option>
            <option value="umbraco">Umbraco Organized</option>
          </select>
        </div>

        <div class="checkbox-group">
          <div class="checkbox-option">
            <input
              type="checkbox"
              id="event-type-in-person"
              class="checkbox-input"
              .checked=${this.showInPerson}
              @change=${this.handleInPersonChange}
            />
            <label for="event-type-in-person" class="checkbox-label">In-person</label>
          </div>
          <div class="checkbox-option">
            <input
              type="checkbox"
              id="event-type-online"
              class="checkbox-input"
              .checked=${this.showOnline}
              @change=${this.handleOnlineChange}
            />
            <label for="event-type-online" class="checkbox-label">Online</label>
          </div>
        </div>

        <button
          class="clear-button"
          @click=${this.handleClearFilters}
          aria-label="Clear all filters"
        >
          Clear Filters
        </button>
      </div>
    `;
  }
}
