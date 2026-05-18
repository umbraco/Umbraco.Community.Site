import { LitElement, html, css } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../../api/not-found-tracker-api.js";

@customElement("not-found-tracker-add-ignore-rule-modal")
export class AddIgnoreRuleModalElement extends LitElement {
  @property({ type: Number }) hitId = 0;
  @property() suggestedPath = "";
  @property() suggestedHostname: string | null = null;
  @state() private matchType: number = 1; // 0 Exact, 1 PathPrefix
  @state() private path = "";
  @state() private hostname: string | null = null;
  @state() private note = "";
  @state() private busy = false;
  @state() private error: string | null = null;

  static styles = css`
    :host { display: block; padding: var(--uui-size-space-4, 16px); min-width: 400px; }
    .field { margin-bottom: var(--uui-size-space-3, 12px); display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    input, select, textarea { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .error { color: red; margin-bottom: 8px; }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.path = this.suggestedPath;
    this.hostname = this.suggestedHostname;
  }

  private async submit() {
    this.busy = true;
    this.error = null;
    try {
      if (this.hitId > 0) {
        await NotFoundTrackerApi.ignoreFromHit(this.hitId, this.path, this.matchType, this.hostname, this.note || null);
      } else {
        await NotFoundTrackerApi.createIgnoreRule(this.path, this.matchType, this.hostname, this.note || null);
      }
      this.dispatchEvent(new CustomEvent("done", { bubbles: true, composed: true }));
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.busy = false;
    }
  }

  private cancel() {
    this.dispatchEvent(new CustomEvent("cancel", { bubbles: true, composed: true }));
  }

  render() {
    return html`
      <h3>Add ignore rule</h3>
      ${this.error ? html`<div class="error">${this.error}</div>` : ""}
      <div class="field">
        <label>Path</label>
        <input
          type="text"
          .value=${this.path}
          @input=${(e: Event) => (this.path = (e.target as HTMLInputElement).value)}
        >
      </div>
      <div class="field">
        <label>Match type</label>
        <select @change=${(e: Event) => (this.matchType = parseInt((e.target as HTMLSelectElement).value))}>
          <option value="0" ?selected=${this.matchType === 0}>Exact</option>
          <option value="1" ?selected=${this.matchType === 1}>Path prefix</option>
        </select>
      </div>
      <div class="field">
        <label>Hostname (optional, leave blank for global)</label>
        <input
          type="text"
          placeholder=${this.suggestedHostname ?? "all sites"}
          .value=${this.hostname ?? ""}
          @input=${(e: Event) => (this.hostname = (e.target as HTMLInputElement).value || null)}
        >
      </div>
      <div class="field">
        <label>Note (optional)</label>
        <textarea rows="2" @input=${(e: Event) => (this.note = (e.target as HTMLTextAreaElement).value)}></textarea>
      </div>
      <div class="actions">
        <button @click=${this.cancel} ?disabled=${this.busy}>Cancel</button>
        <button @click=${this.submit} ?disabled=${this.busy}>${this.busy ? "Saving…" : "Add rule"}</button>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-add-ignore-rule-modal": AddIgnoreRuleModalElement;
  }
}
