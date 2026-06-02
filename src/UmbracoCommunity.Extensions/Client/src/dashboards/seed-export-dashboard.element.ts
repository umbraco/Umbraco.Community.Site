import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";

const BASE_PATH = "/umbraco/umbracocommunityextensions/api/v1/seed";
const PUBLIC_DOWNLOAD_PATH = "/seed/latest.zip";

interface SeedExportStatus {
  isRunning: boolean;
  lastSuccessAt: string | null;
  lastSuccessSizeBytes: number | null;
  lastFailureAt: string | null;
  lastError: string | null;
  startedAt: string | null;
}

@customElement("seed-export-dashboard")
export class SeedExportDashboardElement extends UmbElementMixin(LitElement) {
  @state() private _status: SeedExportStatus | null = null;
  @state() private _loading = false;
  @state() private _triggering = false;
  @state() private _baseUrl = "";

  #tokenSource: string | (() => string | Promise<string>) | undefined;
  #pollHandle: ReturnType<typeof setInterval> | null = null;
  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx;
    });

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      this._baseUrl = config?.base ?? window.location.origin;
      this.#tokenSource = config?.token as
        | string
        | (() => string | Promise<string>)
        | undefined;
      this.#refresh();
    });
  }

  async #authHeader(): Promise<Record<string, string>> {
    if (!this.#tokenSource) return {};
    const token =
      typeof this.#tokenSource === "function"
        ? await this.#tokenSource()
        : this.#tokenSource;
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  connectedCallback() {
    super.connectedCallback();
    this.#pollHandle = setInterval(() => {
      // Poll faster while a regenerate is in flight.
      if (this._status?.isRunning) this.#refresh();
    }, 5_000);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this.#pollHandle) {
      clearInterval(this.#pollHandle);
      this.#pollHandle = null;
    }
  }

  async #refresh() {
    if (!this._baseUrl) return;
    this._loading = true;
    try {
      const res = await fetch(`${this._baseUrl}${BASE_PATH}/status`, {
        headers: await this.#authHeader(),
        credentials: "include",
      });
      if (!res.ok) throw new Error(`Status check failed: ${res.status}`);
      this._status = await res.json();
    } catch (err) {
      console.error("Failed to fetch seed export status", err);
    } finally {
      this._loading = false;
    }
  }

  async #regenerate() {
    if (this._triggering || this._status?.isRunning) return;
    this._triggering = true;
    try {
      const res = await fetch(`${this._baseUrl}${BASE_PATH}/regenerate`, {
        method: "POST",
        headers: await this.#authHeader(),
        credentials: "include",
      });
      if (res.status === 202) {
        this.#notificationContext?.peek("positive", {
          data: { headline: "Snapshot regeneration started", message: "" },
        });
      } else if (res.status === 409) {
        this.#notificationContext?.peek("warning", {
          data: { headline: "A regeneration is already in progress", message: "" },
        });
      } else {
        this.#notificationContext?.peek("danger", {
          data: { headline: `Failed to trigger regeneration (HTTP ${res.status})`, message: "" },
        });
      }
      await this.#refresh();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      this.#notificationContext?.peek("danger", {
        data: { headline: "Failed to trigger regeneration", message },
      });
    } finally {
      this._triggering = false;
    }
  }

  #formatBytes(bytes: number | null): string {
    if (bytes === null) return "—";
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
  }

  #formatRelative(iso: string | null): string {
    if (!iso) return "never";
    const then = new Date(iso).getTime();
    const now = Date.now();
    const seconds = Math.round((now - then) / 1000);
    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.round(seconds / 60)}m ago`;
    if (seconds < 86_400) return `${Math.round(seconds / 3600)}h ago`;
    return `${Math.round(seconds / 86_400)}d ago`;
  }

  #renderStatus() {
    if (!this._status) {
      return html`<p>Loading status…</p>`;
    }
    const s = this._status;
    return html`
      <dl>
        <dt>State</dt>
        <dd>${s.isRunning ? html`<strong>Running</strong> (started ${this.#formatRelative(s.startedAt)})` : "Idle"}</dd>

        <dt>Last success</dt>
        <dd>
          ${s.lastSuccessAt
            ? html`${this.#formatRelative(s.lastSuccessAt)} — ${this.#formatBytes(s.lastSuccessSizeBytes)}
                <br /><small>${new Date(s.lastSuccessAt).toLocaleString()}</small>`
            : "never"}
        </dd>

        ${s.lastFailureAt
          ? html`
              <dt>Last failure</dt>
              <dd>
                ${this.#formatRelative(s.lastFailureAt)}
                <br /><small>${new Date(s.lastFailureAt).toLocaleString()}</small>
                ${s.lastError ? html`<br /><code>${s.lastError}</code>` : null}
              </dd>
            `
          : null}
      </dl>
    `;
  }

  render() {
    const downloadUrl = `${this._baseUrl}${PUBLIC_DOWNLOAD_PATH}`;
    const canDownload = this._status?.lastSuccessAt != null;

    return html`
      <uui-box headline="Snapshot exports">
        <p>
          A full export of the Community site (schema + content + media) runs every 24 hours at
          20:15&nbsp;UTC and is served at the URL below. Contributors use this snapshot to seed
          their local development environment.
        </p>

        ${this.#renderStatus()}

        <div class="actions">
          <uui-button
            look="primary"
            color="default"
            label=${this._triggering || this._status?.isRunning ? "Regenerating…" : "Regenerate now"}
            ?disabled=${this._triggering || this._status?.isRunning || this._loading}
            @click=${() => this.#regenerate()}
          ></uui-button>

          <uui-button
            look="secondary"
            label="Refresh status"
            ?disabled=${this._loading}
            @click=${() => this.#refresh()}
          ></uui-button>
        </div>

        <div class="download">
          <strong>Download URL:</strong>
          ${canDownload
            ? html`<a href=${downloadUrl} target="_blank" rel="noopener">${downloadUrl}</a>`
            : html`<span><code>${downloadUrl}</code> (no snapshot yet)</span>`}
        </div>
      </uui-box>
    `;
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    uui-box {
      max-width: 48rem;
    }

    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.25rem 1rem;
      margin: 1rem 0;
    }

    dt {
      font-weight: 600;
      text-transform: uppercase;
      font-size: 0.75rem;
      letter-spacing: 0.05em;
      color: var(--uui-color-text-alt);
      align-self: center;
    }

    dd {
      margin: 0;
    }

    .actions {
      display: flex;
      gap: 0.5rem;
      margin: 1rem 0;
    }

    .download {
      margin-top: 1rem;
      padding-top: 1rem;
      border-top: 1px solid var(--uui-color-divider);
      word-break: break-all;
    }

    code {
      font-family: var(--uui-font-monospace, monospace);
      background: var(--uui-color-surface-alt);
      padding: 0.1rem 0.3rem;
      border-radius: 3px;
    }
  `;
}

export default SeedExportDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "seed-export-dashboard": SeedExportDashboardElement;
  }
}
