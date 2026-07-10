import { formatMinutes } from "./blog-announcements.helpers.js";
import { LitElement, html, css, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { BlogAnnouncementsApi } from "./api/blog-announcements-api.js";
import type { SettingsResponse } from "./api/blog-announcements-types.js";

@customElement("blog-announcements-settings-tab")
export class BlogAnnouncementsSettingsTabElement extends UmbElementMixin(LitElement) {
  @state() private settings: SettingsResponse | null = null;
  @state() private loading = false;
  @state() private error: string | null = null;
  @state() private testing = false;
  @state() private polling = false;

  private _notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this._notifications = ctx));
  }

  static styles = css`
    :host { display: block; max-width: 640px; }
    .description { color: var(--uui-color-text-alt); margin: 0 0 var(--uui-size-space-5) 0; }
    dl.settings {
      display: grid;
      grid-template-columns: max-content 1fr;
      column-gap: var(--uui-size-space-5);
      row-gap: var(--uui-size-space-3);
      margin: 0 0 var(--uui-size-space-5) 0;
    }
    dl.settings dt { font-weight: 600; color: var(--uui-color-text-alt); }
    dl.settings dd { margin: 0; }
    .error { color: var(--uui-color-danger); }
    uui-box + uui-box { margin-top: var(--uui-size-space-5); }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      this.settings = await BlogAnnouncementsApi.getSettings();
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  private notify(color: "positive" | "warning" | "danger", headline: string, message = "") {
    this._notifications?.peek(color, { data: { headline, message } });
  }

  private async pollNow() {
    this.polling = true;
    try {
      const result = await BlogAnnouncementsApi.pollNow();
      if (result.run) {
        const r = result.run;
        this.notify(
          "positive",
          "Poll complete",
          `Fetched ${r.fetched}, new ${r.new}, announced ${r.announced}, skipped ${r.skipped}, failed ${r.failed}${r.dryRun ? " (dry-run)" : ""}. See the Runs tab for history.`,
        );
      } else {
        this.notify("positive", "Poll complete", "No detection run was recorded — see the Runs tab for history.");
      }
      await this.load();
    } catch (e) {
      this.notify("danger", "Poll failed", (e as Error).message);
    } finally {
      this.polling = false;
    }
  }

  private async sendTest() {
    this.testing = true;
    try {
      const result = await BlogAnnouncementsApi.sendTestMessage();
      if (result.dryRun) {
        this.notify("warning", "Dry-run: nothing posted", "Dry-run mode is on — the test payload was logged but not sent to Discord.");
      } else if (result.outcome === "Success") {
        this.notify("positive", "Test message sent", "Check the Discord channel.");
      } else {
        this.notify("danger", "Test message failed", result.httpStatus ? `Discord returned ${result.httpStatus}.` : "The delivery did not succeed.");
      }
    } catch (e) {
      this.notify("danger", "Test message failed", (e as Error).message);
    } finally {
      this.testing = false;
    }
  }

  render() {
    return html`
      <uui-box headline="Effective configuration">
        <p class="description">
          Read-only snapshot of the pipeline's current config. Editing lives in
          <code>appsettings</code> — this view exists so the state is visible.
        </p>
        ${this.error ? html`<div class="error">${this.error}</div>` : nothing}
        ${this.loading
          ? html`<uui-loader></uui-loader>`
          : this.settings
            ? this.renderSettings(this.settings)
            : nothing}
      </uui-box>

      ${this.settings?.pollNowAvailable
        ? html`
            <uui-box headline="Poll now">
              <p class="description">
                Fetches new posts and runs announcement detection immediately, instead of waiting
                for the next scheduled poll. Runs the exact same cycle as the timer.
              </p>
              <uui-button
                look="primary"
                label=${this.polling ? "Polling…" : "Poll now"}
                state=${this.polling ? "waiting" : nothing}
                ?disabled=${this.polling}
                @click=${() => this.pollNow()}
              ></uui-button>
            </uui-box>
          `
        : nothing}

      <uui-box headline="Test message">
        <p class="description">
          Posts a canned embed to the configured Discord webhook — the first thing to reach for when
          Discord looks quiet. Honours dry-run mode (logs only, posts nothing).
        </p>
        <uui-button
          look="primary"
          label=${this.testing ? "Sending…" : "Send test message"}
          ?disabled=${this.testing}
          @click=${() => this.sendTest()}
        ></uui-button>
      </uui-box>
    `;
  }

  private renderSettings(s: SettingsResponse) {
    return html`
      <dl class="settings">
        <dt>Poll cadence</dt>
        <dd title="How often new posts are fetched — and therefore how often announcements can go out.">
          ${formatMinutes(s.pollIntervalMinutes)}
        </dd>
        <dt>Recency window</dt>
        <dd>${s.recencyWindowDays} day${s.recencyWindowDays === 1 ? "" : "s"}</dd>
        <dt>Per-cycle cap</dt>
        <dd>${s.maxAnnouncementsPerCycle}</dd>
        <dt>Dry-run</dt>
        <dd>
          <uui-tag color=${s.dryRun ? "warning" : "positive"} look="primary">
            ${s.dryRun ? "On (logs only)" : "Off (live)"}
          </uui-tag>
        </dd>
        <dt>Webhook</dt>
        <dd>
          <uui-tag color=${s.webhookConfigured ? "positive" : "danger"} look="primary">
            ${s.webhookConfigured ? "Configured" : "Not configured"}
          </uui-tag>
        </dd>
      </dl>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "blog-announcements-settings-tab": BlogAnnouncementsSettingsTabElement;
  }
}
