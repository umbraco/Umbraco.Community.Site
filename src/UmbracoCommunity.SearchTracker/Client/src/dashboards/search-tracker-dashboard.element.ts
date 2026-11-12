import { LitElement, css, html, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { SearchTrackerApi, setAuthConfig } from "../api/search-tracker-api.js";
import { TermSort, type DailyCount, type SiteItem, type SummaryResponse, type TermStat } from "../api/types.js";
import { formatDay, percent, rangeForDays } from "../util/format.js";
import "./search-daily-chart.element.js";

const PERIODS = [7, 30, 90];
const SORTS = [
  { value: TermSort.Searches, name: "Most searched" },
  { value: TermSort.FewestResults, name: "Fewest results" },
  { value: TermSort.LastSearched, name: "Recently searched" },
  { value: TermSort.Term, name: "A–Z" },
];
const PAGE_SIZE = 25;
// Matches SearchClient.DashboardQueryKey/Value, so checking a term from here isn't counted as a search.
const DASHBOARD_QUERY_KEY = "from";
const DASHBOARD_QUERY_VALUE = "search-tracker";

@customElement("search-tracker-dashboard")
export class SearchTrackerDashboardElement extends UmbElementMixin(LitElement) {
  @state() private sites: SiteItem[] = [];
  @state() private siteKey = "";
  @state() private periodDays = 30;
  @state() private termFilter = "";
  @state() private nothingFoundOnly = false;
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
  private _summaryRequest = 0;
  private _termsRequest = 0;
  private _trendRequest = 0;

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
    .spacer { flex: 1; }
    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: var(--uui-size-space-4);
      margin-bottom: var(--uui-size-space-4);
    }
    .tile {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-2);
      padding: var(--uui-size-space-2) var(--uui-size-space-1);
    }
    .tile-value {
      font-size: var(--uui-type-h3-size, 24px);
      font-weight: 700;
      line-height: 1;
    }
    .tile-label { font-weight: 600; }
    .tile-note { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size, 12px); }
    uui-button uui-icon { margin-right: var(--uui-size-space-1); }
    .trend-heading { margin: 0 0 var(--uui-size-space-1); font-weight: 600; }
    .trend-note { margin: 0 0 var(--uui-size-space-3); color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size, 12px); }
    .muted { color: var(--uui-color-text-alt); }
    uui-box { margin-bottom: var(--uui-size-space-4); }
    .term { font-family: var(--uui-font-monospace, monospace); word-break: break-word; }
    .number { text-align: right; font-variant-numeric: tabular-nums; }
    .row-actions { display: flex; gap: var(--uui-size-space-1); justify-content: flex-end; }
    .terms { width: 100%; border-collapse: collapse; }
    .terms th,
    .terms td {
      padding: var(--uui-size-space-3) var(--uui-size-space-4);
      border-bottom: 1px solid var(--uui-color-border);
      text-align: left;
      vertical-align: middle;
    }
    .terms th { font-weight: 700; white-space: nowrap; }
    .terms tbody tr:hover td { background: var(--uui-color-surface-emphasis); }
    .terms th.number,
    .terms td.number { text-align: right; }
    .terms tbody tr.trend-row:hover td,
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
    const request = ++this._summaryRequest;
    try {
      const summary = await SearchTrackerApi.getSummary(this.range);
      if (request === this._summaryRequest) this.summary = summary;
    } catch (e) {
      if (request === this._summaryRequest) this.error = (e as Error).message;
    }
  }

  private async loadTerms() {
    const request = ++this._termsRequest;
    this.loading = true;
    this.error = null;
    try {
      const result = await SearchTrackerApi.getTerms({
        ...this.range,
        contains: this.termFilter.trim() || undefined,
        nothingFoundOnly: this.nothingFoundOnly,
        sort: this.sort,
        skip: this.skip,
        take: PAGE_SIZE,
      });
      if (request !== this._termsRequest) return;
      this.terms = result.items;
      this.total = result.total;
    } catch (e) {
      if (request === this._termsRequest) this.error = (e as Error).message;
    } finally {
      if (request === this._termsRequest) this.loading = false;
    }
  }

  private async toggleTrend(item: TermStat) {
    if (this.expandedTerm === item.term) {
      this.expandedTerm = null;
      return;
    }
    const request = ++this._trendRequest;
    this.expandedTerm = item.term;
    this.trend = [];
    try {
      const trend = await SearchTrackerApi.getTermTrend(item.term, this.range);
      if (request === this._trendRequest) this.trend = trend;
    } catch (e) {
      if (request === this._trendRequest) this.error = (e as Error).message;
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
          <div class="tile">
            <div class="tile-value">${s.totalSearches.toLocaleString()}</div>
            <div class="tile-label">Searches</div>
            <div class="tile-note">In the last ${this.periodDays} days</div>
          </div>
        </uui-box>
        <uui-box>
          <div class="tile">
            <div class="tile-value">${s.uniqueTerms.toLocaleString()}</div>
            <div class="tile-label">Different terms</div>
            <div class="tile-note">${s.uniqueTerms ? `${(s.totalSearches / s.uniqueTerms).toLocaleString(undefined, { maximumFractionDigits: 1 })} searches per term on average` : "No terms yet"}</div>
          </div>
        </uui-box>
        <uui-box>
          <div class="tile">
            <div class="tile-value">${s.zeroResultSearches.toLocaleString()}</div>
            <div class="tile-label">Searches that found nothing</div>
            <div class="tile-note">${percent(s.zeroResultSearches, s.totalSearches)} of all searches</div>
          </div>
        </uui-box>
      </div>
      <uui-box headline="Searches per day, ${formatDay(s.from)} – ${formatDay(s.to)}">
        <search-tracker-daily-chart .days=${s.days}></search-tracker-daily-chart>
      </uui-box>
    `;
  }

  private renderTable() {
    return html`
      <table class="terms" aria-label="Search terms">
        <thead><tr>
          <th scope="col">Term</th>
          <th scope="col" class="number">Searches</th>
          <th scope="col" class="number" title="Number of results the most recent search for this term returned">Results count (latest)</th>
          <th scope="col">Last searched</th>
          <th scope="col"><span class="sr-only">Actions</span></th>
        </tr></thead><tbody>
        ${this.terms.length === 0 && !this.loading
          ? html`
              <tr>
                <td colspan="5"><div class="empty">No searches logged for this period.</div></td>
              </tr>
            `
          : this.terms.map(
              (item) => html`
                <tr>
                  <td class="term">${item.term}</td>
                  <td class="number">${item.searches.toLocaleString()}</td>
                  <td class="number">${this.renderResultCount(item.latestResultCount)}</td>
                  <td>${formatDay(item.lastSearched)}</td>
                  <td>
                    <div class="row-actions">
                      <uui-button
                        compact
                        look="secondary"
                        label=${this.expandedTerm === item.term ? "Hide trend" : "Show trend"}
                        aria-expanded=${this.expandedTerm === item.term ? "true" : "false"}
                        @click=${() => this.toggleTrend(item)}
                      >
                        <uui-icon name=${this.expandedTerm === item.term ? "icon-navigation-up" : "icon-chart-curve"}></uui-icon>
                        ${this.expandedTerm === item.term ? "Hide trend" : "Show trend"}
                      </uui-button>
                      ${this.renderResultsLinks(item)}
                      <uui-button
                        compact
                        look="secondary"
                        color="danger"
                        label="Delete"
                        @click=${() => this.deleteTerm(item)}
                      ></uui-button>
                    </div>
                  </td>
                </tr>
                ${this.expandedTerm === item.term
                  ? html`
                      <tr class="trend-row">
                        <td class="trend-cell" colspan="5">
                          <p class="trend-heading">Searches per day for “${item.term}”</p>
                          <p class="trend-note">
                            ${this.trend.length ? html`${formatDay(this.trend[0].day)} – ${formatDay(this.trend[this.trend.length - 1].day)}. ` : nothing}
                            ${item.latestResultCount === null
                              ? "The number of results wasn't recorded for this term's latest search."
                              : html`Its latest search returned ${item.latestResultCount === 1 ? "1 result" : `${item.latestResultCount.toLocaleString()} results`}.`}
                          </p>
                          ${this.trend.length
                            ? html`<search-tracker-daily-chart .days=${this.trend} height="90"></search-tracker-daily-chart>`
                            : html`<uui-loader></uui-loader>`}
                        </td>
                      </tr>
                    `
                  : nothing}
              `,
            )}
      </tbody></table>
    `;
  }

  private renderResultsLinks(item: TermStat) {
    const sites = item.siteKeys
      .map((key) => this.sites.find((s) => s.key === key))
      .filter((s): s is SiteItem & { searchUrl: string } => !!s?.searchUrl);
    const named = sites.length > 1;

    return sites.map((site) => {
      const url = new URL(site.searchUrl, window.location.origin);
      url.searchParams.set("q", item.term);
      url.searchParams.set(DASHBOARD_QUERY_KEY, DASHBOARD_QUERY_VALUE);
      const text = named ? `View on ${site.name}` : "View results";
      return html`
        <uui-button
          compact
          look="secondary"
          href=${url.toString()}
          target="_blank"
          rel="noopener"
          label=${`${text} for “${item.term}” on ${site.name} (opens in a new tab)`}
        >
          <uui-icon name="icon-out"></uui-icon>
          ${text}
        </uui-button>
      `;
    });
  }

  private renderResultCount(count: number | null) {
    if (count === null) return html`<span class="muted" title="Not recorded for this search">–</span>`;
    if (count === 0) return html`<uui-tag color="danger" look="secondary">None</uui-tag>`;
    return count.toLocaleString();
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
    const showOptions = [
      { name: "All terms", value: "all", selected: !this.nothingFoundOnly },
      { name: "Only terms that found nothing", value: "nothing", selected: this.nothingFoundOnly },
    ];

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
        <div class="field">
          <uui-label for="show">Show</uui-label>
          <uui-select
            id="show"
            label="Show"
            .options=${showOptions}
            @change=${(e: Event) => { this.nothingFoundOnly = (e.target as HTMLSelectElement).value === "nothing"; this.skip = 0; this.loadTerms(); }}
          ></uui-select>
        </div>
        <div class="spacer"></div>
        <uui-button
          look="secondary"
          label=${this.loading ? "Refreshing…" : "Refresh"}
          ?disabled=${this.loading}
          @click=${() => { this.loadSites(); this.loadAll(); }}
        >
          <uui-icon name="icon-refresh"></uui-icon>
          ${this.loading ? "Refreshing…" : "Refresh"}
        </uui-button>
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
