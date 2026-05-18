import { LitElement, html, css } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../../api/not-found-tracker-api.js";

@customElement("not-found-tracker-create-redirect-modal")
export class CreateRedirectModalElement extends LitElement {
  @property({ type: Number }) hitId = 0;
  @property() hitPath = "";
  @state() private targetKey = "";
  @state() private culture = "";
  @state() private busy = false;
  @state() private error: string | null = null;

  static styles = css`
    :host { display: block; padding: var(--uui-size-space-4, 16px); min-width: 400px; }
    .field { margin-bottom: var(--uui-size-space-3, 12px); display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    input { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .error { color: red; margin-bottom: 8px; }
    small { color: var(--uui-color-text-alt, #686c87); }
  `;

  private async submit() {
    if (!this.targetKey) {
      this.error = "Target content key is required.";
      return;
    }
    this.busy = true;
    this.error = null;
    try {
      await NotFoundTrackerApi.createRedirect(this.hitId, this.targetKey, this.culture || null);
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
      <h3>Create redirect</h3>
      <p>Redirect from <strong>${this.hitPath}</strong> to a content node.</p>
      ${this.error ? html`<div class="error">${this.error}</div>` : ""}
      <div class="field">
        <label>Target content key (GUID)</label>
        <input
          type="text"
          placeholder="00000000-0000-0000-0000-000000000000"
          .value=${this.targetKey}
          @input=${(e: Event) => (this.targetKey = (e.target as HTMLInputElement).value)}
        >
        <small>Paste the content node's key from its Info tab. Will be replaced with Umbraco's native content picker in a follow-up.</small>
      </div>
      <div class="field">
        <label>Culture (optional)</label>
        <input
          type="text"
          placeholder="en-us"
          .value=${this.culture}
          @input=${(e: Event) => (this.culture = (e.target as HTMLInputElement).value)}
        >
      </div>
      <div class="actions">
        <button @click=${this.cancel} ?disabled=${this.busy}>Cancel</button>
        <button @click=${this.submit} ?disabled=${this.busy}>${this.busy ? "Saving…" : "Create redirect"}</button>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-create-redirect-modal": CreateRedirectModalElement;
  }
}
