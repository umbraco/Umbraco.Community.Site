import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { Event, EventFilters } from '@umbraco-community/types/events-calendar/event';
import { EventService } from '@umbraco-community/services/events-calendar/event-service';
import { createLogger } from '@umbraco-community/util/logger';
import { isEventFetchError } from '@umbraco-community/types/events-calendar/errors';

const logger = createLogger('events-calendar');

// Import all components
import './events-calendar-filters';
import './events-calendar-map';
import './events-calendar-table';

type LoadingState = 'idle' | 'loading' | 'error' | 'success';

@customElement('events-calendar-app')
export class EventsCalendarApp extends LitElement {
  @property({ type: Boolean, reflect: true, attribute: 'data-show-map' }) showMap = false;
  @property({ type: Number, attribute: 'data-page-size' }) configuredPageSize = 50;
  @property({ type: String, attribute: 'data-google-maps-api-key' }) googleMapsApiKey = '';
  @property({ type: String, attribute: 'data-google-maps-map-id' }) googleMapsMapId = '';

  @state() private allEvents: Event[] = [];
  @state() private filteredEvents: Event[] = [];
  @state() private currentFilters: EventFilters = {
    searchQuery: '',
    startDate: '',
    endDate: '',
    showInPerson: false,
    showOnline: false,
    organizationType: 'all',
  };
  @state() private loadingState: LoadingState = 'loading';
  @state() private errorMessage = '';
  @state() private pageSize = 50;

  private eventService = new EventService();

  constructor() {
    super();
    // Ensure loading state is set immediately
    this.loadingState = 'loading';
  }

  static styles = css`
    :host {
      display: block;
      max-width: 85rem;
      margin: 0 auto;
      font-family: var(--font-family);
    }

    .app-container {
      padding: 0;
    }

    .main-content {
      display: flex;
      flex-direction: column;
    }

    /* Loading States */
    .loading-container {
      display: flex;
      flex-direction: column;
      gap: var(--unit-md);
      padding: var(--unit-md);
    }

    .error-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--unit-sm);
      padding: var(--unit-xl);
      text-align: center;
    }

    .error-message {
      color: var(--color-identity-dark);
      font-size: var(--font-size);
    }

    .retry-button {
      background-color: #283a97;
      color: var(--color-identity-white);
      border: none;
      border-radius: var(--border-radius);
      padding: var(--unit-xs) var(--unit-md);
      font-size: var(--font-size);
      cursor: pointer;
      transition: background-color 0.2s ease;
    }

    .retry-button:hover {
      background-color: #1e2a7a;
    }

    /* Skeleton UI */
    .skeleton {
      background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
      background-size: 200% 100%;
      animation: loading 1.5s infinite;
    }

    @keyframes loading {
      0% {
        background-position: 200% 0;
      }
      100% {
        background-position: -200% 0;
      }
    }

    .skeleton-header {
      height: 3.75rem;
      border-radius: var(--border-radius);
      margin-bottom: var(--unit-md);
    }

    .skeleton-filters {
      height: 3.125rem;
      border-radius: var(--border-radius);
      margin-bottom: var(--unit-md);
    }

    .skeleton-map {
      height: 25rem;
      border-radius: var(--border-radius);
      margin-bottom: var(--unit-md);
    }

    .skeleton-table {
      display: flex;
      flex-direction: column;
      gap: var(--unit-xs);
    }

    .skeleton-table-header {
      height: 2.5rem;
      border-radius: var(--border-radius);
    }

    .skeleton-table-row {
      height: 3.75rem;
      border-radius: var(--border-radius);
    }

    /* Screen reader announcements */
    .sr-only {
      position: absolute;
      width: 0.0625rem;
      height: 0.0625rem;
      padding: 0;
      margin: -0.0625rem;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .map-legend {
      display: flex;
      justify-content: flex-end;
      gap: var(--unit-sm);
      font-size: var(--font-size-sm);
      font-style: italic;
      color: var(--color-identity-dark);
    }

    @media (min-width: 769px) {
      .map-legend {
        margin-top: -1rem;
        margin-bottom: 1rem;
      }
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 0.375rem;
    }

    .legend-circle {
      width: 0.625rem;
      height: 0.625rem;
      border-radius: 50%;
      display: inline-block;
    }

    .legend-circle.community {
      background-color: #1b264f;
    }

    .legend-circle.umbraco {
      background-color: #283a97;
    }

    /* Responsive layout */
    @media (max-width: 768px) {
      :host {
        padding: var(--unit-sm);
      }

      .skeleton-map {
        height: 18.75rem;
      }

      .map-legend {
        flex-direction: column;
        align-items: flex-start;
        margin-bottom: 1rem;
        gap: 0;
      }
    }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.loadEvents();
  }

  private async loadEvents() {
    this.loadingState = 'loading';
    this.errorMessage = '';

    try {
      await this.eventService.fetchEvents();
      this.allEvents = this.eventService.getAllEvents();

      // Use configured page size from property
      this.pageSize = this.configuredPageSize || 50;

      this.applyFilters();
      this.loadingState = 'success';
    } catch (error) {
      this.loadingState = 'error';

      if (isEventFetchError(error)) {
        this.errorMessage = error.message;
        logger.error('Failed to load events', {
          message: error.message,
          statusCode: error.statusCode,
          retryable: error.retryable
        });
      } else {
        this.errorMessage = 'Failed to load events. Please try again later.';
        logger.error('Failed to load events', { error: error instanceof Error ? error.message : String(error) });
      }
    }
  }

  private async retryLoad() {
    await this.loadEvents();
  }

  private applyFilters() {
    this.filteredEvents = this.eventService.filterEvents(this.allEvents, this.currentFilters);
  }

  private handleFilterChange(event: CustomEvent) {
    this.currentFilters = { ...event.detail };
    this.applyFilters();
  }

  private handleEventSelected(event: CustomEvent) {
    const selectedEvent = event.detail as Event;

    // If event has an external link, open it in a new tab
    if (selectedEvent.link) {
      window.open(selectedEvent.link, '_blank', 'noopener,noreferrer');
    }
    // Otherwise, do nothing (event is not clickable)
  }

  render() {
    return html`
      <div class="app-container">
        <main class="main-content">
          <!-- Accessibility announcements -->
          <div aria-live="polite" aria-atomic="true" class="sr-only">
            ${this.getLoadingAnnouncement()}
          </div>
          
          ${this.renderContent()}
        </main>
      </div>
    `;
  }

  private getLoadingAnnouncement(): string {
    switch (this.loadingState) {
      case 'loading':
        return 'Loading events...';
      case 'error':
        return `Error loading events: ${this.errorMessage}`;
      case 'success':
        return `Loaded ${this.allEvents.length} events`;
      default:
        return '';
    }
  }

  private renderContent() {
    switch (this.loadingState) {
      case 'loading':
        return this.renderLoadingSkeleton();
      case 'error':
        return this.renderError();
      case 'success':
        return this.renderLoadedContent();
      default:
        return this.renderLoadingSkeleton();
    }
  }

  private renderLoadingSkeleton() {
    return html`
      <div class="loading-container">
        <!-- Search filters skeleton -->
        <div class="skeleton skeleton-filters"></div>

        <!-- Map skeleton (only if showMap is true) -->
        ${this.showMap ? html`<div class="skeleton skeleton-map"></div>` : ''}

        <!-- Table skeleton -->
        <div class="skeleton-table">
          <div class="skeleton skeleton-table-header"></div>
          <div class="skeleton skeleton-table-row"></div>
          <div class="skeleton skeleton-table-row"></div>
          <div class="skeleton skeleton-table-row"></div>
          <div class="skeleton skeleton-table-row"></div>
          <div class="skeleton skeleton-table-row"></div>
        </div>
      </div>
    `;
  }

  private renderError() {
    return html`
      <div class="error-container">
        <div class="error-message">${this.errorMessage}</div>
        <button 
          class="retry-button"
          @click=${this.retryLoad}
          aria-label="Retry loading events"
        >
          Try Again
        </button>
        
        <!-- Graceful degradation: Show search filters even when data fails -->
        <div style="margin-top: var(--spacing-xl); max-width: 37.5rem;">
          <p style="color: var(--color-text-secondary); font-size: var(--font-size-small); text-align: center; margin-bottom: var(--spacing-md);">
            You can still use the search filters once data loads:
          </p>
          <events-calendar-filters
            .searchQuery=${this.currentFilters.searchQuery}
            .startDate=${this.currentFilters.startDate}
            .endDate=${this.currentFilters.endDate}
            .showInPerson=${this.currentFilters.showInPerson}
            .showOnline=${this.currentFilters.showOnline}
            @filter-change=${this.handleFilterChange}
          ></events-calendar-filters>
        </div>
      </div>
    `;
  }

  private renderLoadedContent() {
    const mapMarkers = this.eventService.getMapMarkers(this.filteredEvents);

    return html`
      <events-calendar-filters
        .searchQuery=${this.currentFilters.searchQuery}
        .startDate=${this.currentFilters.startDate}
        .endDate=${this.currentFilters.endDate}
        .showInPerson=${this.currentFilters.showInPerson}
        .showOnline=${this.currentFilters.showOnline}
        .organizationType=${this.currentFilters.organizationType}
        @filter-change=${this.handleFilterChange}
      ></events-calendar-filters>

      ${this.showMap ? html`
        <events-calendar-map
          .markers=${mapMarkers}
        ></events-calendar-map>
        <div class="map-legend">
          <span class="legend-item"><span class="legend-circle community"></span> Community Event</span>
          <span class="legend-item"><span class="legend-circle umbraco"></span> Umbraco Organized</span>
        </div>
      ` : ''}

      <events-calendar-table
        .events=${this.filteredEvents}
        .pageSize=${this.pageSize}
        @event-selected=${this.handleEventSelected}
      ></events-calendar-table>
    `;
  }
}



// Vite import.meta.env type declaration for TypeScript
declare global {
  interface ImportMeta {
    env: {
      DEV: boolean;
      [key: string]: string | boolean | undefined;
    };
  }
}
