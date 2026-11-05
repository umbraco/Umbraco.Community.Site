import { LitElement, css, html, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { SearchTrackerApi, setAuthConfig } from "../api/search-tracker-api.js";
import { TermSort, type DailyCount, type SiteItem, type SummaryResponse, type TermStat } from "../api/types.js";
import { formatDay, percent, rangeForDays } from "../util/format.js";
import "./search-daily-chart.element.js";

const PERIODS = [7, 30, 90, 365];
const SORTS = [
  { value: TermSort.Searches, name: "Most searched" },
  { value: TermSort.ZeroResults, name: "Most searches with no results" },
  { value: TermSort.LastSearched, name: "Recently searched" },
  { value: TermSort.Term, name: "A–Z" },
];
const PAGE_SIZE = 25;

@customElement("search-tracker-dashboard")
export class SearchTrackerDashboardElement extends UmbElementMixin(LitElement) {
  @state() private sites: SiteItem[] = [];
  @state() private siteKey = "";
  @state() private periodDays = 30;
  @state() private termFilter = "";
  @state() private zeroResultsOnly = false;
  @state() private sort = TermSort.Searches;
  @state() private skip = 0;

  @state() private summary: SummaryResponse | null = null;
  @state() private terms: TermStat[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private error: string | null = null;

  @state() private expandedTerm: string | null = null;
  @state() private trend: DailyCount[] = [];

  private _authReady = false;

  constructor() {
    super();
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      setAuthConfig({
        token: () => authContext.getLatestToken(),
        baseUrl: authContext.getOpenApiConfiguration().base,
        credentials: authContext.getOpenApiConfiguration().credentials as RequestCredentials,
      });
      if (!this._authReady) {
        this._authReady = true;
        this.loadSites();
        this.loadAll();
      }
    });
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-space-5, 20px);
    }
    .toolbar {
      display: flex;
      gap: var(--uui-size-space-3);
      align-items: end;
      flex-wrap: wrap;
      margin-bottom: var(--uui-size-space-4);
    }
    .field { display: flex; flex-direction: column; gap: var(--uui-size-1); min-width: 160px; }
    .field > uui-input,
    .field > uui-select { width: 100%; }
    .toggle-field { align-self: center; padding-top: var(--uui-size-space-4); }
    .spacer { flex: 1; }
    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: var(--uui-size-space-4);
      margin-bottom: var(--uui-size-space-4);
    }
    .tile-value {
      font-size: var(--uui-type-h3-size, 24px);
      font-weight: 700;
    }
    .tile-label { color: var(--uui-color-text-alt); }
    uui-box { margin-bottom: var(--uui-size-space-4); }
    .term { font-family: var(--uui-font-monospace, monospace); word-break: break-word; }
    .number { text-align: right; font-variant-numeric: tabular-nums; }
    .row-actions { display: flex; gap: var(--uui-size-space-1); justify-content: flex-end; }
    .trend-cell { background: var(--uui-color-surface-alt); }
    .empty {
      text-align: center;
      padding: var(--uui-size-space-6);
      color: var(--uui-color-text-alt);
    }
    .pagination {
      display: flex;
      gap: var(--uui-size-space-3);
      align-items: center;
      margin-top: var(--uui-size-space-4);
    }
    .error-banner {
      color: var(--uui-color-danger);
      background: var(--uui-color-danger-emphasis, #fde7e7);
      padding: var(--uui-size-space-3);
      border-radius: var(--uui-border-radius);
      margin-bottom: var(--uui-size-space-3);
    }
    .note { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size, 12px); }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
  `;

  private get range() {
    return { siteKey: this.siteKey || undefined, ...rangeForDays(this.periodDays) };
  }

  private async loadSites() {
    try {
      this.sites = await SearchTrackerApi.getSites();
    } catch {
      this.sites = [];
    }
  }

  private async loadAll() {
    this.expandedTerm = null;
    await Promise.all([this.loadSummary(), this.loadTerms()]);
  }

  private async loadSummary() {
    try {
      this.summary = await SearchTrackerApi.getSummary(this.range);
    } catch (e) {
      this.error = (e as Error).message;
    }
  }

  private async loadTerms() {
    this.loading = true;
    this.error = null;
    try {
      const result = await SearchTrackerApi.getTerms({
        ...this.range,
        contains: this.termFilter.trim() || undefined,
        zeroResultsOnly: this.zeroResultsOnly,
        sort: this.sort,
        skip: this.skip,
        take: PAGE_SIZE,
      });
      this.terms = result.items;
      this.total = result.total;
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  private async toggleTrend(item: TermStat) {
    if (this.expandedTerm === item.term) {
      this.expandedTerm = null;
      return;
    }
    this.expandedTerm = item.term;
    this.trend = [];
    try {
      this.trend = await SearchTrackerApi.getTermTrend(item.term, this.range);
    } catch (e) {
      this.error = (e as Error).message;
    }
  }

  private async deleteTerm(item: TermStat) {
    const siteName = this.sites.find((s) => s.key === this.siteKey)?.name;
    try {
      await umbConfirmModal(this, {
        headline: "Delete this search term?",
        content: html`
          <p>This permanently removes every logged search for <strong>${item.term}</strong>
          ${siteName ? html`on <strong>${siteName}</strong>` : "on all sites you can access"}, for all dates.</p>
          <p>Use this to remove terms that contain personal information.</p>
        `,
        color: "danger",
        confirmLabel: "Delete term",
      });
    } catch {
      return;
    }
    try {
      await SearchTrackerApi.deleteTerm(item.term, this.siteKey || undefined);
      await this.loadAll();
    } catch (e) {
      this.error = (e as Error).message;
    }
  }

  private resetAndLoad() {
    this.skip = 0;
    this.loadAll();
  }

  private renderTiles() {
    const s = this.summary;
    if (!s) return nothing;
    return html`
      <div class="tiles">
        <uui-box>
          <div class="tile-value">${s.totalSearches.toLocaleString()}</div>
          <div class="tile-label">Searches</div>
        </uui-box>
        <uui-box>
          <div class="tile-value">${s.uniqueTerms.toLocaleString()}</div>
          <div class="tile-label">Different terms</div>
        </uui-box>
        <uui-box>
          <div class="tile-value">${s.zeroResultSearches.toLocaleString()}
            <small>(${percent(s.zeroResultSearches, s.totalSearches)})</small></div>
          <div class="tile-label">Searches with no results</div>
        </uui-box>
      </div>
      <uui-box headline="Searches per day">
        <search-tracker-daily-chart .days=${s.days}></search-tracker-daily-chart>
      </uui-box>
    `;
  }

  private renderTable() {
    return html`
      <uui-table aria-label="Search terms">
        <uui-table-head>
          <uui-table-head-cell>Term</uui-table-head-cell>
          <uui-table-head-cell class="number">Searches</uui-table-head-cell>
          <uui-table-head-cell class="number">No results</uui-table-head-cell>
          <uui-table-head-cell>Last searched</uui-table-head-cell>
          <uui-table-head-cell><span class="sr-only">Actions</span></uui-table-head-cell>
        </uui-table-head>
        ${this.terms.length === 0 && !this.loading
          ? html`
              <uui-table-row>
                <uui-table-cell colspan="5"><div class="empty">No searches logged for this period.</div></uui-table-cell>
              </uui-table-row>
            `
          : this.terms.map(
              (item) => html`
                <uui-table-row>
                  <uui-table-cell class="term">${item.term}</uui-table-cell>
                  <uui-table-cell class="number">${item.searches.toLocaleString()}</uui-table-cell>
                  <uui-table-cell class="number">${item.zeroResultSearches.toLocaleString()}</uui-table-cell>
                  <uui-table-cell>${formatDay(item.lastSearched)}</uui-table-cell>
                  <uui-table-cell>
                    <div class="row-actions">
                      <uui-button
                        compact
                        look="secondary"
                        label=${this.expandedTerm === item.term ? "Hide trend" : "Trend"}
                        aria-expanded=${this.expandedTerm === item.term ? "true" : "false"}
                        @click=${() => this.toggleTrend(item)}
                      ></uui-button>
                      <uui-button
                        compact
                        look="secondary"
                        color="danger"
                        label="Delete"
                        @click=${() => this.deleteTerm(item)}
                      ></uui-button>
                    </div>
                  </uui-table-cell>
                </uui-table-row>
                ${this.expandedTerm === item.term
                  ? html`
                      <uui-table-row>
                        <uui-table-cell class="trend-cell" colspan="5">
                          ${this.trend.length
                            ? html`<search-tracker-daily-chart .days=${this.trend} height="48"></search-tracker-daily-chart>`
                            : html`<uui-loader></uui-loader>`}
                        </uui-table-cell>
                      </uui-table-row>
                    `
                  : nothing}
              `,
            )}
      </uui-table>
    `;
  }

  private renderPagination() {
    if (this.total <= PAGE_SIZE) return nothing;
    const page = Math.floor(this.skip / PAGE_SIZE) + 1;
    const pages = Math.ceil(this.total / PAGE_SIZE);
    return html`
      <div class="pagination">
        <uui-button
          look="secondary"
          label="Previous"
          ?disabled=${page <= 1}
          @click=${() => { this.skip = Math.max(0, this.skip - PAGE_SIZE); this.loadTerms(); }}
        ></uui-button>
        <span>Page ${page} of ${pages} (${this.total.toLocaleString()} terms)</span>
        <uui-button
          look="secondary"
          label="Next"
          ?disabled=${page >= pages}
          @click=${() => { this.skip += PAGE_SIZE; this.loadTerms(); }}
        ></uui-button>
      </div>
    `;
  }

  render() {
    const siteOptions = [
      { name: "All sites", value: "", selected: this.siteKey === "" },
      ...this.sites.map((s) => ({ name: s.name, value: s.key, selected: this.siteKey === s.key })),
    ];
    const periodOptions = PERIODS.map((d) => ({
      name: `Last ${d} days`,
      value: String(d),
      selected: this.periodDays === d,
    }));
    const sortOptions = SORTS.map((s) => ({ name: s.name, value: String(s.value), selected: this.sort === s.value }));

    return html`
      <div class="toolbar">
        <div class="field">
          <uui-label for="site">Site</uui-label>
          <uui-select
            id="site"
            label="Site"
            .options=${siteOptions}
            @change=${(e: Event) => { this.siteKey = (e.target as HTMLSelectElement).value; this.resetAndLoad(); }}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label for="period">Period</uui-label>
          <uui-select
            id="period"
            label="Period"
            .options=${periodOptions}
            @change=${(e: Event) => { this.periodDays = parseInt((e.target as HTMLSelectElement).value, 10); this.resetAndLoad(); }}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label for="contains">Filter terms</uui-label>
          <uui-input
            id="contains"
            label="Filter terms"
            type="search"
            placeholder="Term contains…"
            .value=${this.termFilter}
            @input=${(e: Event) => (this.termFilter = (e.target as HTMLInputElement).value)}
            @change=${() => { this.skip = 0; this.loadTerms(); }}
          ></uui-input>
        </div>
        <div class="field">
          <uui-label for="sort">Sort</uui-label>
          <uui-select
            id="sort"
            label="Sort"
            .options=${sortOptions}
            @change=${(e: Event) => { this.sort = parseInt((e.target as HTMLSelectElement).value, 10); this.skip = 0; this.loadTerms(); }}
          ></uui-select>
        </div>
        <uui-toggle
          class="toggle-field"
          label="Only terms with no results"
          .checked=${this.zeroResultsOnly}
          @change=${(e: Event) => { this.zeroResultsOnly = (e.target as HTMLInputElement).checked; this.skip = 0; this.loadTerms(); }}
        ></uui-toggle>
        <div class="spacer"></div>
        <uui-button
          look="secondary"
          label=${this.loading ? "Refreshing…" : "Refresh"}
          ?disabled=${this.loading}
          @click=${() => { this.loadSites(); this.loadAll(); }}
        ></uui-button>
      </div>

      ${this.error ? html`<div class="error-banner" role="alert">${this.error}</div>` : nothing}

      ${this.renderTiles()}

      <uui-box headline="Search terms">
        ${this.loading ? html`<uui-loader></uui-loader>` : nothing}
        ${this.renderTable()}
        ${this.renderPagination()}
        <p class="note">
          Terms are stored lower-cased, with email addresses and long numbers masked. Days are in UTC.
          Bots, repeated searches and page 2+ of results aren't counted.
        </p>
      </uui-box>
    `;
  }
}

export default SearchTrackerDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "search-tracker-dashboard": SearchTrackerDashboardElement;
  }
}
