import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { client } from "../api/client.gen.js";

// Local type definitions until OpenAPI types are regenerated
interface ContributionStats {
  totalExternalPullRequests: number;
  totalExternalContributors: number;
  startDate: string;
  endDate: string;
  topContributors: ContributorDetail[];
}

interface ContributorDetail {
  login: string;
  name: string;
  pullRequestCount: number;
}

interface ReleaseSummary {
  startDate: string;
  endDate: string;
  releases: ReleaseInfo[];
}

interface ReleaseInfo {
  version: string;
  releaseDate: string | null;
  isLts: boolean;
  isMajor: boolean;
  isPreRelease: boolean;
  url: string;
  totalPullRequests: number;
  externalPullRequests: number;
  externalContributors: number;
  topContributors: ContributorDetail[];
}

@customElement("contribution-stats-dashboard")
export class ContributionStatsDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _contributionStats: ContributionStats | null = null;

  @state()
  private _releases: ReleaseSummary | null = null;

  @state()
  private _isLoading = false;

  @state()
  private _startDate: string = "";

  @state()
  private _endDate: string = "";

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  readonly #API_SECURITY = [{ scheme: 'bearer', type: 'http' }] as const;
  readonly #START_YEAR = 2012;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.#setYearRange(new Date().getFullYear());
    this.#loadData();
  }

  async #loadData() {
    if (!this._startDate || !this._endDate) return;

    this._isLoading = true;

    try {
      const [contributionData, releaseData] = await Promise.all([
        this.#fetchApi<ContributionStats>('contributions'),
        this.#fetchApi<ReleaseSummary>('releases')
      ]);

      this._contributionStats = contributionData;
      this._releases = releaseData;
    } catch (error) {
      console.error("Failed to load dashboard data", error);
      this.#showNotification("danger", "Failed to load data", String(error));
    } finally {
      this._isLoading = false;
    }
  }

  async #fetchApi<T>(endpoint: string): Promise<T> {
    const { data, error } = await client.get({
      url: `/umbraco/umbracocommunitygithubusers/api/v1/${endpoint}?startDate=${this._startDate}&endDate=${this._endDate}`,
      security: this.#API_SECURITY,
    });

    if (error) {
      throw new Error(`Failed to load ${endpoint}: ${String(error)}`);
    }

    return data as T;
  }

  #showNotification(
    color: "positive" | "warning" | "danger" | "default",
    headline: string,
    message?: string
  ) {
    if (this.#notificationContext) {
      this.#notificationContext.peek(color, {
        data: {
          headline,
          message: message || "",
        },
      });
    }
  }

  #onDateChange(field: "start" | "end", event: Event) {
    const value = (event.target as HTMLInputElement).value;
    if (field === "start") {
      this._startDate = value;
    } else {
      this._endDate = value;
    }
  }

  #setYearRange(year: number) {
    this._startDate = `${year}-01-01`;
    this._endDate = `${year}-12-31`;
  }

  #onSetYear(year: number) {
    this.#setYearRange(year);
    this.#loadData();
  }

  #getHistoricalYears(): number[] {
    const currentYear = new Date().getFullYear();
    return Array.from(
      { length: currentYear - 1 - this.#START_YEAR + 1 },
      (_, i) => currentYear - 1 - i
    );
  }

  #formatDate(dateStr: string | null): string {
    if (!dateStr) return "Not set";
    return new Date(dateStr).toLocaleDateString();
  }

  #getReleaseUrl(version: string): string {
    return `/release/umbraco/Umbraco-CMS/${version}`;
  }

  #renderGitHubLink(login: string, displayName: string) {
    return html`<a href="https://github.com/${login}" target="_blank" rel="noopener">${displayName}</a>`;
  }

  #renderLtsBadge(isLts: boolean) {
    return isLts ? html`<span class="badge lts">LTS</span>` : '';
  }

  #renderContributionStats() {
    if (!this._contributionStats) return null;

    return html`
      <uui-box headline="External Contributions Summary (Umbraco CMS)">
        <div class="stats-grid">
          <div class="stat-card">
            <div class="stat-value">${this._contributionStats.totalExternalPullRequests}</div>
            <div class="stat-label">Merged Pull Requests</div>
          </div>
          <div class="stat-card">
            <div class="stat-value">${this._contributionStats.totalExternalContributors}</div>
            <div class="stat-label">Unique Contributors</div>
          </div>
        </div>

        ${this._contributionStats.topContributors.length > 0
          ? html`
              <div class="top-contributors">
                <h3>Top Contributors</h3>
                <uui-table>
                  <uui-table-head>
                    <uui-table-head-cell>Contributor</uui-table-head-cell>
                    <uui-table-head-cell>Merged Pull Requests</uui-table-head-cell>
                  </uui-table-head>
                  ${this._contributionStats.topContributors.map(
                    (contributor) => html`
                      <uui-table-row>
                        <uui-table-cell>
                          ${this.#renderGitHubLink(contributor.login, contributor.name)}
                        </uui-table-cell>
                        <uui-table-cell>
                          <strong>${contributor.pullRequestCount}</strong>
                        </uui-table-cell>
                      </uui-table-row>
                    `
                  )}
                </uui-table>
              </div>
            `
          : html`<p class="empty-message">No external contributions in this date range.</p>`}
      </uui-box>
    `;
  }

  #renderReleases() {
    if (!this._releases) return null;

    const majorReleases = this._releases.releases.filter((r) => r.isMajor);

    return html`
      <uui-box headline="Major Releases with Community Contributions">
        ${majorReleases.length > 0
          ? html`
              ${majorReleases.map(
                (release) => html`
                  <div class="release-section">
                    <div class="release-header">
                      <h3>
                        <a href="${this.#getReleaseUrl(release.version)}" target="_blank" rel="noopener">
                          ${release.version}
                        </a>
                        ${this.#renderLtsBadge(release.isLts)}
                      </h3>
                      <span class="release-date">${this.#formatDate(release.releaseDate)}</span>
                    </div>
                    <div class="release-stats">
                      <div class="stat-item">
                        <strong>${release.externalPullRequests}</strong> external PRs
                      </div>
                      <div class="stat-item">
                        <strong>${release.externalContributors}</strong> contributors
                      </div>
                      <div class="stat-item">
                        <strong>${release.totalPullRequests}</strong> total PRs
                      </div>
                    </div>
                    ${release.topContributors.length > 0
                      ? html`
                          <div class="release-contributors">
                            <strong>Top contributors:</strong>
                            ${release.topContributors.map(
                              (c) => html`
                                <a
                                  href="https://github.com/${c.login}"
                                  target="_blank"
                                  rel="noopener"
                                  class="contributor-link"
                                >
                                  ${c.name} (${c.pullRequestCount})
                                </a>
                              `
                            )}
                          </div>
                        `
                      : html`<p class="empty-text">No external contributions</p>`}
                  </div>
                `
              )}
            `
          : html`<p class="empty-message">No major releases in this date range.</p>`}
      </uui-box>

      <uui-box headline="All Releases">
        ${this._releases.releases.length > 0
          ? html`
              <uui-table>
                <uui-table-head>
                  <uui-table-head-cell>Version</uui-table-head-cell>
                  <uui-table-head-cell>Release Date</uui-table-head-cell>
                  <uui-table-head-cell>Type</uui-table-head-cell>
                  <uui-table-head-cell>External PRs</uui-table-head-cell>
                  <uui-table-head-cell>Contributors</uui-table-head-cell>
                </uui-table-head>
                ${this._releases.releases.map(
                  (release) => html`
                    <uui-table-row>
                      <uui-table-cell>
                        <a href="${this.#getReleaseUrl(release.version)}" target="_blank" rel="noopener">
                          ${release.version}
                        </a>
                      </uui-table-cell>
                      <uui-table-cell>${this.#formatDate(release.releaseDate)}</uui-table-cell>
                      <uui-table-cell>
                        ${release.isPreRelease
                          ? html`<span class="badge prerelease">Pre-release</span>`
                          : release.isMajor
                          ? html`<span class="badge major">Major</span>`
                          : html`<span class="badge minor">Minor/Patch</span>`}
                        ${this.#renderLtsBadge(release.isLts)}
                      </uui-table-cell>
                      <uui-table-cell>${release.externalPullRequests}</uui-table-cell>
                      <uui-table-cell>${release.externalContributors}</uui-table-cell>
                    </uui-table-row>
                  `
                )}
              </uui-table>
            `
          : html`<p class="empty-message">No releases in this date range.</p>`}
      </uui-box>
    `;
  }

  render() {
    return html`
      <div class="dashboard-container">
        <div class="dashboard-header">
          <h1>Umbraco-CMS Community Contributions</h1>
        </div>

        <uui-box>
          <div class="date-filter">
            <div class="date-inputs">
              <uui-form-layout-item>
                <uui-label for="start-date" slot="label">Start Date</uui-label>
                <input
                  id="start-date"
                  type="date"
                  class="date-input"
                  .value=${this._startDate}
                  @change=${(e: Event) => this.#onDateChange("start", e)}
                />
              </uui-form-layout-item>

              <uui-form-layout-item>
                <uui-label for="end-date" slot="label">End Date</uui-label>
                <input
                  id="end-date"
                  type="date"
                  class="date-input"
                  .value=${this._endDate}
                  @change=${(e: Event) => this.#onDateChange("end", e)}
                />
              </uui-form-layout-item>
            </div>

            <div class="button-group">
              <uui-button look="primary" @click=${() => this.#loadData()} ?disabled=${this._isLoading}>
                Load Data
              </uui-button>
              <uui-button look="secondary" @click=${() => this.#onSetYear(new Date().getFullYear())}>
                Current Year
              </uui-button>
              <uui-button look="secondary" @click=${() => this.#onSetYear(new Date().getFullYear() - 1)}>
                Previous Year
              </uui-button>
              ${this.#getHistoricalYears().map(
                (year) => html`
                  <uui-button look="secondary" @click=${() => this.#onSetYear(year)}>
                    ${year}
                  </uui-button>
                `
              )}
            </div>
          </div>
        </uui-box>

        ${this._isLoading
          ? html`<uui-loader></uui-loader>`
          : html`
              ${this.#renderContributionStats()}
              ${this.#renderReleases()}
            `}
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .dashboard-container {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-5);
      }

      .dashboard-header h1 {
        margin: 0;
        font-size: var(--uui-type-h3-size);
      }

      .date-filter {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-4);
      }

      .date-inputs {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: var(--uui-size-space-4);
      }

      .date-input {
        width: 100%;
        padding: var(--uui-size-space-2);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        font-family: inherit;
        font-size: inherit;
      }

      .button-group {
        display: flex;
        gap: var(--uui-size-space-3);
        flex-wrap: wrap;
      }

      .stats-grid {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-5);
      }

      .release-section {
        padding: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-4);
        background-color: var(--uui-color-surface);
        border-radius: var(--uui-border-radius);
        border: 1px solid var(--uui-color-border);
      }

      .release-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: var(--uui-size-space-3);
      }

      .release-header h3 {
        margin: 0;
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .release-date {
        color: var(--uui-color-text-alt);
        font-size: 0.9rem;
      }

      .release-stats {
        display: flex;
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-3);
        padding: var(--uui-size-space-3);
        background-color: var(--uui-color-surface-alt);
        border-radius: var(--uui-border-radius);
      }

      .stat-item {
        color: var(--uui-color-text-alt);
      }

      .release-contributors {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
        align-items: center;
      }

      .contributor-link {
        color: var(--uui-color-interactive);
        text-decoration: none;
        padding: 2px 8px;
        background-color: var(--uui-color-surface);
        border-radius: 3px;
        border: 1px solid var(--uui-color-border);
      }

      .contributor-link:hover {
        background-color: var(--uui-color-surface-alt);
        text-decoration: none;
      }

      .stat-card {
        text-align: center;
        padding: var(--uui-size-space-4);
        background-color: var(--uui-color-surface);
        border-radius: var(--uui-border-radius);
      }

      .stat-value {
        font-size: 2.5rem;
        font-weight: bold;
        color: var(--uui-color-interactive);
      }

      .stat-label {
        margin-top: var(--uui-size-space-2);
        color: var(--uui-color-text-alt);
        font-size: 0.9rem;
      }

      .top-contributors {
        margin-top: var(--uui-size-space-5);
      }

      .top-contributors h3 {
        margin-top: 0;
        margin-bottom: var(--uui-size-space-3);
      }

      .empty-message {
        color: var(--uui-color-text-alt);
        font-style: italic;
        padding: var(--uui-size-space-4);
        text-align: center;
      }

      .empty-text {
        color: var(--uui-color-text-alt);
        font-style: italic;
        margin: 0;
      }

      .badge {
        display: inline-block;
        padding: 2px 8px;
        border-radius: 3px;
        font-size: 0.85rem;
        margin-right: var(--uui-size-space-2);
      }

      .badge.major {
        background-color: var(--uui-color-focus);
        color: white;
      }

      .badge.minor {
        background-color: var(--uui-color-surface);
        color: var(--uui-color-text);
      }

      .badge.prerelease {
        background-color: var(--uui-color-warning);
        color: var(--uui-color-warning-contrast);
      }

      .badge.lts {
        background-color: var(--uui-color-positive);
        color: white;
      }

      uui-loader {
        display: flex;
        justify-content: center;
        padding: var(--uui-size-layout-3);
      }

      a {
        color: var(--uui-color-interactive);
        text-decoration: none;
      }

      a:hover {
        text-decoration: underline;
      }

      @media (max-width: 768px) {
        .stats-grid {
          grid-template-columns: 1fr;
        }

        .date-inputs {
          grid-template-columns: 1fr;
        }

        .button-group {
          flex-direction: column;
        }
      }
    `,
  ];
}

export default ContributionStatsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "contribution-stats-dashboard": ContributionStatsDashboardElement;
  }
}
