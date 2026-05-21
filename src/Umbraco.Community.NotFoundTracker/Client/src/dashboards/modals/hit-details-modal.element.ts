import { html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { NotFoundTrackerApi } from "../../api/not-found-tracker-api.js";
import type { HitDetail } from "../../api/types.js";
import type { HitDetailsModalData, HitDetailsModalValue } from "./hit-details-modal.token.js";

const STATUS_LABELS = ["Active", "Ignored", "Redirected"];

@customElement("not-found-tracker-hit-details-modal")
export class HitDetailsModalElement extends UmbModalBaseElement<HitDetailsModalData, HitDetailsModalValue> {
  @state() private detail: HitDetail | null = null;
  @state() private loading = false;
  @state() private error: string | null = null;

  static styles = css`
    .meta {
      display: grid;
      grid-template-columns: max-content 1fr;
      column-gap: var(--uui-size-space-4);
      row-gap: var(--uui-size-space-2);
      margin: 0 0 var(--uui-size-space-5) 0;
    }
    .meta dt {
      font-weight: 600;
      color: var(--uui-color-text-alt);
    }
    .meta dd {
      margin: 0;
      word-break: break-word;
    }
    .section-title {
      font-weight: 600;
      margin: 0 0 var(--uui-size-space-3) 0;
    }
    .qs-table {
      width: 100%;
      border-collapse: collapse;
    }
    .qs-table th,
    .qs-table td {
      text-align: left;
      padding: var(--uui-size-space-2) var(--uui-size-space-3);
      border-bottom: 1px solid var(--uui-color-divider);
      vertical-align: top;
    }
    .qs-table th {
      color: var(--uui-color-text-alt);
      font-weight: 600;
      font-size: 0.9em;
    }
    .qs-table td.qs-string {
      word-break: break-all;
      font-family: var(--uui-font-monospace, monospace);
      font-size: 0.9em;
    }
    .empty {
      color: var(--uui-color-text-alt);
      font-style: italic;
    }
    .error {
      color: var(--uui-color-danger);
    }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    if (!this.data) return;
    this.loading = true;
    this.error = null;
    try {
      this.detail = await NotFoundTrackerApi.getHit(this.data.hitId);
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  render() {
    return html`
      <umb-body-layout headline="Hit details">
        <uui-box>
          ${this.error ? html`<div class="error">${this.error}</div>` : nothing}
          ${this.loading
            ? html`<uui-loader></uui-loader>`
            : this.detail
              ? this.renderDetail(this.detail)
              : nothing}
        </uui-box>

        <uui-button
          slot="actions"
          look="secondary"
          label="Close"
          @click=${() => this._submitModal()}
        ></uui-button>
      </umb-body-layout>
    `;
  }

  private renderDetail(d: HitDetail) {
    return html`
      <dl class="meta">
        <dt>Path</dt>
        <dd>${d.path}</dd>
        <dt>Hostname</dt>
        <dd>${d.hostname}</dd>
        <dt>Status</dt>
        <dd>${STATUS_LABELS[d.status]}</dd>
        <dt>Hits</dt>
        <dd>${d.hitCount}</dd>
        <dt>First seen</dt>
        <dd>${new Date(d.firstSeenUtc).toLocaleString()}</dd>
        <dt>Last seen</dt>
        <dd>${new Date(d.lastSeenUtc).toLocaleString()}</dd>
        ${d.lastUserAgent
          ? html`
            <dt>Last user agent</dt>
            <dd>${d.lastUserAgent}</dd>
          `
          : nothing}
      </dl>

      <h4 class="section-title">
        Query strings${d.queryStrings.length > 0 ? html` (${d.queryStrings.length})` : nothing}
      </h4>
      ${d.queryStrings.length === 0
        ? html`<div class="empty">No query strings recorded for this path.</div>`
        : html`
          <table class="qs-table">
            <thead>
              <tr>
                <th>Query string</th>
                <th>Hits</th>
                <th>Last seen</th>
              </tr>
            </thead>
            <tbody>
              ${d.queryStrings.map(
                (qs) => html`
                  <tr>
                    <td class="qs-string">${qs.queryString}</td>
                    <td>${qs.hitCount}</td>
                    <td>${new Date(qs.lastSeenUtc).toLocaleString()}</td>
                  </tr>
                `,
              )}
            </tbody>
          </table>
        `}
    `;
  }
}

export default HitDetailsModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-hit-details-modal": HitDetailsModalElement;
  }
}
