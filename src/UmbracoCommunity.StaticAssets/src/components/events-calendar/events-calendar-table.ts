import { LitElement, html, css, PropertyValues } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { Event } from '@umbraco-community/types/events-calendar/event';
import { EventService } from '@umbraco-community/services/events-calendar/event-service';

type TabType = 'upcoming' | 'past';

@customElement('events-calendar-table')
export class EventsCalendarTable extends LitElement {
  @property({ type: Array }) events: Event[] = [];
  @property({ type: Number }) pageSize = 50;
  @state() private activeTab: TabType = 'upcoming';
  @state() private currentPage = 1;
  @state() private isPageLoading = false;

  private eventService = new EventService();
  private pageLoadTimer: number | null = null;
  private pendingPage: number | null = null;
  private readonly pageTransitionDelay = 300;

  static styles = css`
    :host {
      display: block;
    }

    .tabs-container {
      border-bottom: 0.0625rem solid var(--color-light-grey);
      margin-bottom: 0;
    }

    .tabs {
      display: flex;
      gap: 0;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .tab {
      padding: var(--unit-sm) var(--unit-sm);
      font-size: var(--font-size);
      font-weight: var(--font-weight-medium);
      cursor: pointer;
      border: none;
      background: transparent;
      color: var(--color-identity-dark);
      border-bottom: 0.125rem solid transparent;
      transition: all 0.2s ease;
      font-family: var(--font-family);
      min-width: 6.25rem;
      text-align: center;
    }

    .tab:hover {
      color: var(--color-identity-darkest);
    }

    .tab:focus {
      outline: none;
      box-shadow: 0 0 0 0.125rem var(--color-identity-blue);
    }

    .tab.active {
      background-color: #283a97;
      color: var(--color-identity-white);
      border-radius: var(--border-radius) var(--border-radius) 0 0;
    }

    .table-container {
      overflow-x: auto;
    }

    .table {
      width: 100%;
      min-width: 50rem; /* Ensure table has minimum width for all columns */
      border-collapse: collapse;
      font-size: var(--font-size);
    }

    .table-header {
      background-color: transparent;
    }

    .table-header th {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-identity-dark);
      text-transform: uppercase;
      letter-spacing: 0.03125rem;
      padding: var(--unit-sm) var(--unit-xs);
      border-bottom: 0.0625rem solid var(--color-light-grey);
      text-align: left;
      vertical-align: middle;
    }

    .table-header th:nth-child(1) { width: 7.5rem; } /* Start Date */
    .table-header th:nth-child(2) { width: 7.5rem; } /* End Date */
    .table-header th:nth-child(3) { width: auto; }   /* Event Name */
    .table-header th:nth-child(4) { width: 9.375rem; } /* Location */
    .table-header th:nth-child(5) { width: 12.5rem; } /* Type */

    .table-header th.center {
      text-align: center;
    }

    .table-row {
      border-bottom: 0.0625rem solid var(--color-bg-gray);
      transition: background-color 0.2s ease;
    }

    .table-row:nth-child(odd) {
      background-color: #283a971a;
    }


    .table-cell {
      padding: var(--unit-sm) var(--unit-xs);
      color: var(--color-identity-darkest);
      vertical-align: middle;
    }

    .table-cell.center {
      text-align: center;
    }

    .event-name {
      color: var(--color-identity-blue);
      text-decoration: none;
      font-weight: var(--font-weight-medium);
    }


    .event-name:focus {
      outline: 0.125rem solid var(--color-identity-blue);
      outline-offset: 0.125rem;
    }

    .event-subtitle {
      width: 0.75rem;
      height: 0.75rem;
      border-radius: 50%;
      display: inline-block;
    }

    .event-subtitle.community-event {
      background-color: #1b264f;
    }

    .event-subtitle.umbraco-event {
      background-color: #283a97;
    }

    .pagination {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 0.5rem;
      margin-top: var(--unit-sm);
      font-size: var(--font-size);
      font-family: var(--font-family);
      border-top: 1px solid var(--color-light-grey);
      padding-top: var(--unit-sm);
    }

    .pagination-link {
      background: none;
      border: none;
      padding: 0;
      font: inherit;
      cursor: pointer;
      color: var(--color-identity-darkest);
      transition: color 0.2s ease;
    }

    .pagination-link:hover:not(.disabled) {
      color: #283a97;
    }

    .pagination-link.active {
      color: #283a97;
      font-weight: var(--font-weight-semibold);
      cursor: default;
    }

    .pagination-link.disabled {
      color: var(--color-identity-dark);
      cursor: text;
    }

    .pagination-separator {
      color: var(--color-identity-dark);
    }

    .pagination-ellipsis {
      color: var(--color-identity-dark);
      letter-spacing: 0.125rem;
    }

    .pagination-loading {
      padding: var(--unit-md) 0;
      text-align: center;
      color: var(--color-identity-dark);
      font-style: italic;
    }

    .space-indicator {
      display: inline-block;
      width: 0.5rem;
      height: 0.5rem;
      border-radius: 50%;
      background-color: var(--color-identity-blue);
    }

    .empty-state {
      text-align: center;
      padding: var(--unit-xl);
      color: var(--color-identity-dark);
    }

    .empty-state-title {
      font-size: var(--font-size-h2);
      font-weight: var(--font-weight-medium);
      margin-bottom: var(--unit-xs);
    }

    .empty-state-description {
      font-size: var(--font-size);
    }

    /* Responsive design */
    @media (max-width: 768px) {
      .table-cell {
        padding: var(--unit-xs);
      }
    }

    /* Mobile responsiveness - enable horizontal scrolling instead of hiding columns */
    @media (max-width: 768px) {
      .table-container {
        -webkit-overflow-scrolling: touch; /* Smooth scrolling on iOS */
      }
    }

    #upcoming-panel {
      margin-bottom: 2.5rem;
    }
  `;

  disconnectedCallback(): void {
    super.disconnectedCallback();
    this.resetPageLoadingState();
  }

  protected willUpdate(changedProperties: PropertyValues<this>): void {
    super.willUpdate(changedProperties);

    if (changedProperties.has('events') || changedProperties.has('pageSize')) {
      this.resetPageLoadingState();
      this.currentPage = 1;
      return;
    }

    const totalPages = this.getTotalPages(this.filteredEvents);
    if (totalPages === 0 && this.currentPage !== 1) {
      this.resetPageLoadingState();
      this.currentPage = 1;
    } else if (totalPages > 0 && this.currentPage > totalPages) {
      this.resetPageLoadingState();
      this.currentPage = totalPages;
    }
  }

  private get filteredEvents(): Event[] {
    const today = new Date().toISOString().split('T')[0] || '';

    if (this.activeTab === 'upcoming') {
      const upcoming = this.events.filter(event => event.date >= today);
      return upcoming;
    } else {
      // Calculate date 2 years ago
      const twoYearsAgo = new Date();
      twoYearsAgo.setFullYear(twoYearsAgo.getFullYear() - 2);
      const twoYearsAgoStr = twoYearsAgo.toISOString().split('T')[0] || '';

      // Filter past events: must be in the past AND not older than 2 years
      const past = this.events.filter(event => {
        const effectiveDate = event.endDate || event.date;
        return effectiveDate < today && effectiveDate >= twoYearsAgoStr;
      });

      return past;
    }
  }

  private handleTabClick(tab: TabType) {
    if (this.activeTab === tab) {
      this.focusTab(tab);
      return;
    }

    this.activeTab = tab;
    this.currentPage = 1;
    this.focusTab(tab);
  }

  private handleTabKeydown(tab: TabType, event: KeyboardEvent) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.handleTabClick(tab);
      return;
    }

    if (event.key === 'ArrowRight') {
      event.preventDefault();
      const targetTab = tab === 'upcoming' ? 'past' : 'upcoming';
      this.handleTabClick(targetTab);
      return;
    }

    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      const targetTab = tab === 'past' ? 'upcoming' : 'past';
      this.handleTabClick(targetTab);
    }
  }

  private focusTab(tab: TabType) {
    void this.updateComplete.then(() => {
      const tabButton = this.shadowRoot?.getElementById(`${tab}-tab`) as HTMLButtonElement | null;
      tabButton?.focus();
    });
  }

  private handlePaginationKeydown(event: KeyboardEvent) {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
      return;
    }

    const target = event.currentTarget as HTMLElement | null;
    if (!target) {
      return;
    }

    event.preventDefault();

    const buttons = Array.from(
      this.shadowRoot?.querySelectorAll<HTMLButtonElement>('.pagination .pagination-link:not(.disabled)') ?? []
    );

    const currentIndex = buttons.indexOf(target as HTMLButtonElement);
    if (currentIndex === -1) {
      return;
    }

    const direction = event.key === 'ArrowRight' ? 1 : -1;
    const nextIndex = currentIndex + direction;

    if (nextIndex < 0 || nextIndex >= buttons.length) {
      return;
    }

    buttons[nextIndex].focus();
  }

  /**
   * Get event organization type - Community vs Umbraco Organized
   */
  private getEventType(event: Event): string {
    // RSS events are community events (meetups, user groups)
    if (event.source === 'rss') {
      return 'Community Event';
    }
    
    // Default to Umbraco organized for Airtable events or events without source
    return 'Umbraco Organized';
  }

  /**
   * Render event name - external link for events with links, internal click for others
   */
  private renderEventName(event: Event) {
    // Events with external links - open in new tab
    if (event.link && event.link.length > 0) {
      return html`
        <a
          href="${event.link}"
          target="_blank"
          rel="noopener noreferrer"
          class="event-name external-link"
          aria-label="Open ${event.name} externally (opens in new tab)"
        >
          ${event.name}
        </a>
      `;
    }

    // Events without external links - render as plain text (not clickable)
    return html`
      <span class="event-name-text">
        ${event.name}
      </span>
    `;
  }

  /**
   * Get CSS class for subtitle based on content
   */
  private getSubtitleClass(subtitle: string): string {
    if (subtitle === 'Community Event') {
      return 'community-event';
    }
    if (subtitle === 'Umbraco Organized Event') {
      return 'umbraco-event';
    }
    return '';
  }

  /**
   * Get CSS class for event type badge
   */
  private getEventTypeClass(eventType: string): string {
    if (eventType === 'Community Event') {
      return 'community-event';
    }
    if (eventType === 'Umbraco Organized') {
      return 'umbraco-event';
    }
    return '';
  }

  private renderEmptyState() {
    const tabName = this.activeTab === 'upcoming' ? 'upcoming' : 'past';
    
    return html`
      <div class="empty-state">
        <div class="empty-state-title">No ${tabName} events found</div>
        <div class="empty-state-description">
          ${this.activeTab === 'upcoming' 
            ? 'There are no upcoming events matching your current filters.'
            : 'There are no past events matching your current filters.'
          }
        </div>
      </div>
    `;
  }

  private renderTableRows() {
    const events = this.filteredEvents;
    const totalPages = this.getTotalPages(events);

    if (this.isPageLoading) {
      return html`
        <div class="table-container">
          <table
            class="table"
            role="table"
            aria-busy="true"
            aria-label="Events calendar table"
          >
            <caption class="sr-only">List of upcoming and past events</caption>
            <thead class="table-header">
              <tr>
                <th scope="col">Start Date</th>
                <th scope="col">End Date</th>
                <th scope="col">Event Name</th>
                <th scope="col">Location</th>
                <th scope="col" class="center">Type</th>
              </tr>
            </thead>
            <tbody>
              <tr class="table-row">
                <td class="table-cell" colspan="5">
                  <div class="pagination-loading" role="status" aria-live="polite">
                    Loading events…
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        ${this.renderPaginationControls(totalPages)}
      `;
    }

    if (events.length === 0) {
      return this.renderEmptyState();
    }

    const safePageSize = Math.max(1, this.pageSize);
    const startIndex = (this.currentPage - 1) * safePageSize;
    const pagedEvents = events.slice(startIndex, startIndex + safePageSize);

    return html`
      <div class="table-container">
        <table
          class="table"
          role="table"
          aria-label="Events calendar table"
        >
          <caption class="sr-only">List of upcoming and past events</caption>
          <thead class="table-header">
            <tr>
              <th scope="col">Start Date</th>
              <th scope="col">End Date</th>
              <th scope="col">Event Name</th>
              <th scope="col">Location</th>
              <th scope="col" class="center">Type</th>
            </tr>
          </thead>
          <tbody>
            ${pagedEvents.map(event => html`
              <tr class="table-row">
                <td class="table-cell">
                  ${this.eventService.formatDate(event.date)}
                </td>
                <td class="table-cell">
                  ${event.endDate ? this.eventService.formatDate(event.endDate) : '-'}
                </td>
                <td class="table-cell">
                  ${this.renderEventName(event)}
                </td>
                <td class="table-cell">
                  ${event.displayLocation || (event.location.country ? `${event.location.city}, ${event.location.country}` : event.location.city)}
                </td>
                <td class="table-cell center">
                  <div class="event-subtitle ${this.getEventTypeClass(this.getEventType(event))}"></div>
                </td>
              </tr>
            `)}
          </tbody>
        </table>
      </div>
      ${this.renderPaginationControls(totalPages)}
    `;
  }

  private renderPaginationControls(totalPages: number) {
    if (totalPages <= 1) {
      return null;
    }

    const activePage = this.pendingPage ?? this.currentPage;
    const sequence = this.generatePaginationSequence(totalPages, activePage);
    const isFirstPage = activePage === 1;
    const isLastPage = activePage === totalPages;

    const renderSeparator = () => html`<span class="pagination-separator" aria-hidden="true">|</span>`;

    return html`
      <div
        class="pagination"
        role="navigation"
        aria-label="Event pages"
      >
        <button
          type="button"
          class="pagination-link ${isFirstPage ? 'disabled' : ''}"
          ?disabled=${isFirstPage}
          @click=${this.handlePreviousPage}
          @keydown=${this.handlePaginationKeydown}
        >
          Previous
        </button>
        ${renderSeparator()}
        ${sequence.map((item, index) => {
          if (item === 'ellipsis') {
            return html`
              <span class="pagination-ellipsis" aria-hidden="true">…</span>
              ${index < sequence.length - 1 ? renderSeparator() : null}
            `;
          }

          const isActive = item === activePage;
          return html`
            <button
              type="button"
              class="pagination-link ${isActive ? 'active' : ''}"
              aria-current=${isActive ? 'page' : 'false'}
              @click=${() => this.handlePageChange(item as number)}
              @keydown=${this.handlePaginationKeydown}
            >
              ${item}
            </button>
            ${index < sequence.length - 1 ? renderSeparator() : null}
          `;
        })}
        ${renderSeparator()}
        <button
          type="button"
          class="pagination-link ${isLastPage ? 'disabled' : ''}"
          ?disabled=${isLastPage}
          @click=${this.handleNextPage}
          @keydown=${this.handlePaginationKeydown}
        >
          Next
        </button>
      </div>
    `;
  }

  private getTotalPages(events: Event[]): number {
    if (!events.length) {
      return 0;
    }

    const safePageSize = Math.max(1, this.pageSize);
    return Math.ceil(events.length / safePageSize);
  }

  private handlePageChange(page: number) {
    const totalPages = this.getTotalPages(this.filteredEvents);
    if (page < 1 || page > totalPages || page === this.currentPage) {
      return;
    }
    this.startPageTransition(page);
  }

  private handlePreviousPage = () => {
    if (this.currentPage > 1) {
      this.startPageTransition(this.currentPage - 1);
    }
  };

  private handleNextPage = () => {
    const totalPages = this.getTotalPages(this.filteredEvents);
    if (this.currentPage < totalPages) {
      this.startPageTransition(this.currentPage + 1);
    }
  };

  private generatePaginationSequence(totalPages: number, activePage: number): Array<number | 'ellipsis'> {
    const pages = new Set<number>();

    pages.add(1);
    pages.add(totalPages);

    const windowSize = 1;

    for (let offset = -windowSize; offset <= windowSize; offset += 1) {
      const candidate = activePage + offset;
      if (candidate > 1 && candidate < totalPages) {
        pages.add(candidate);
      }
    }

    if (activePage <= 3) {
      for (let page = 1; page <= Math.min(5, totalPages); page += 1) {
        pages.add(page);
      }
    }

    if (activePage >= totalPages - 2) {
      for (let page = totalPages; page >= Math.max(totalPages - 4, 1); page -= 1) {
        pages.add(page);
      }
    }

    const sorted = Array.from(pages).filter(page => page > 0 && page <= totalPages).sort((a, b) => a - b);

    const sequence: Array<number | 'ellipsis'> = [];
    for (let i = 0; i < sorted.length; i += 1) {
      const current = sorted[i];
      const previous = sorted[i - 1];

      if (previous !== undefined && current - previous > 1) {
        sequence.push('ellipsis');
      }

      sequence.push(current);
    }

    return sequence;
  }

  private startPageTransition(targetPage: number) {
    const totalPages = this.getTotalPages(this.filteredEvents);
    if (targetPage < 1 || targetPage > totalPages) {
      return;
    }

    this.resetPageLoadingState();
    this.pendingPage = targetPage;
    this.isPageLoading = true;

    this.pageLoadTimer = window.setTimeout(() => {
      if (this.pendingPage !== null) {
        this.currentPage = this.pendingPage;
      }
      this.resetPageLoadingState();
    }, this.pageTransitionDelay);
  }

  private resetPageLoadingState() {
    if (this.pageLoadTimer !== null) {
      window.clearTimeout(this.pageLoadTimer);
      this.pageLoadTimer = null;
    }
    this.pendingPage = null;
    this.isPageLoading = false;
  }

  render() {
    return html`
      <div class="tabs-container">
        <div class="tabs" role="tablist">
          <button
            class="tab ${this.activeTab === 'upcoming' ? 'active' : ''}"
            id="upcoming-tab"
            role="tab"
            aria-selected=${this.activeTab === 'upcoming'}
            aria-controls="upcoming-panel"
            @click=${() => this.handleTabClick('upcoming')}
            @keydown=${(e: KeyboardEvent) => this.handleTabKeydown('upcoming', e)}
          >
            Upcoming
          </button>
          <button
            class="tab ${this.activeTab === 'past' ? 'active' : ''}"
            id="past-tab"
            role="tab"
            aria-selected=${this.activeTab === 'past'}
            aria-controls="past-panel"
            @click=${() => this.handleTabClick('past')}
            @keydown=${(e: KeyboardEvent) => this.handleTabKeydown('past', e)}
          >
            Past
          </button>
        </div>
      </div>

      <div
        id="upcoming-panel"
        role="tabpanel"
        aria-labelledby="upcoming-tab"
        ?hidden=${this.activeTab !== 'upcoming'}
      >
        ${this.activeTab === 'upcoming' ? this.renderTableRows() : ''}
      </div>

      <div
        id="past-panel"
        role="tabpanel"
        aria-labelledby="past-tab"
        ?hidden=${this.activeTab !== 'past'}
      >
        ${this.activeTab === 'past' ? this.renderTableRows() : ''}
      </div>
    `;
  }
}
