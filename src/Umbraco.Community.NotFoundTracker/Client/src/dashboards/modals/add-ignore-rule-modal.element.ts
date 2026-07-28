import { html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { NotFoundTrackerApi } from "../../api/not-found-tracker-api.js";
import type { AddIgnoreRuleModalData, AddIgnoreRuleModalValue } from "./add-ignore-rule-modal.token.js";

@customElement("not-found-tracker-add-ignore-rule-modal")
export class AddIgnoreRuleModalElement extends UmbModalBaseElement<AddIgnoreRuleModalData, AddIgnoreRuleModalValue> {
  @state() private matchType: number = 1; // 0 Exact, 1 PathPrefix
  @state() private path = "";
  @state() private hostname: string | null = null;
  @state() private hostnames: string[] = [];
  @state() private note = "";
  @state() private busy = false;
  @state() private error: string | null = null;

  static styles = css`
    .field { margin-bottom: var(--uui-size-space-4); }
    .field > uui-input,
    .field > uui-select,
    .field > uui-textarea { width: 100%; }
    .error { color: var(--uui-color-danger); margin-bottom: var(--uui-size-space-3); }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.path = this.data?.suggestedPath ?? "";
    this.hostname = this.data?.suggestedHostname ?? null;
    this.loadHostnames();
  }

  private async loadHostnames() {
    try {
      this.hostnames = await NotFoundTrackerApi.getHostnames();
    } catch {
      // Non-fatal — if this fails the dropdown still shows All sites + suggested.
    }
  }

  private hostnameOptions() {
    // Merge: server-known hostnames + the suggested hostname (defensive — in case
    // the suggestion was recorded *just now* and isn't yet in the cached list).
    const known = new Set(this.hostnames);
    if (this.data?.suggestedHostname) known.add(this.data.suggestedHostname);
    return [
      { name: "All sites", value: "", selected: this.hostname === null },
      ...[...known].sort().map((h) => ({ name: h, value: h, selected: this.hostname === h })),
    ];
  }

  private async submit() {
    if (!this.path) {
      this.error = "Path is required.";
      return;
    }
    this.busy = true;
    this.error = null;
    try {
      const hitId = this.data?.hitId ?? 0;
      if (hitId > 0) {
        await NotFoundTrackerApi.ignoreFromHit(hitId, this.path, this.matchType, this.hostname, this.note || null);
      } else {
        await NotFoundTrackerApi.createIgnoreRule(this.path, this.matchType, this.hostname, this.note || null);
      }
      this._submitModal();
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.busy = false;
    }
  }

  render() {
    return html`
      <umb-body-layout headline="Add ignore rule">
        <uui-box>
          ${this.error ? html`<div class="error">${this.error}</div>` : nothing}

          <div class="field">
            <uui-label for="path" required>Path</uui-label>
            <uui-input
              id="path"
              .value=${this.path}
              @input=${(e: Event) => (this.path = (e.target as HTMLInputElement).value)}
            ></uui-input>
          </div>

          <div class="field">
            <uui-label for="match-type">Match type</uui-label>
            <uui-select
              id="match-type"
              .options=${[
                { name: "Exact", value: "0", selected: this.matchType === 0 },
                { name: "Path prefix", value: "1", selected: this.matchType === 1 },
              ]}
              @change=${(e: Event) => (this.matchType = parseInt((e.target as HTMLSelectElement).value))}
            ></uui-select>
          </div>

          <div class="field">
            <uui-label for="hostname">Hostname</uui-label>
            <uui-select
              id="hostname"
              .options=${this.hostnameOptions()}
              @change=${(e: Event) => {
                const value = (e.target as HTMLSelectElement).value;
                this.hostname = value === "" ? null : value;
              }}
            ></uui-select>
          </div>

          <div class="field">
            <uui-label for="note">Note (optional)</uui-label>
            <uui-textarea
              id="note"
              rows="2"
              .value=${this.note}
              @input=${(e: Event) => (this.note = (e.target as HTMLTextAreaElement).value)}
            ></uui-textarea>
          </div>
        </uui-box>

        <uui-button
          slot="actions"
          look="secondary"
          label="Cancel"
          ?disabled=${this.busy}
          @click=${() => this._rejectModal()}
        ></uui-button>
        <uui-button
          slot="actions"
          look="primary"
          color="positive"
          label=${this.busy ? "Saving…" : "Add rule"}
          ?disabled=${this.busy}
          @click=${this.submit}
        ></uui-button>
      </umb-body-layout>
    `;
  }
}

export default AddIgnoreRuleModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-add-ignore-rule-modal": AddIgnoreRuleModalElement;
  }
}
