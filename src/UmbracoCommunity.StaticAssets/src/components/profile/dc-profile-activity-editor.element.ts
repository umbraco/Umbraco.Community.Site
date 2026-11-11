import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import {
  MAX_ACTIVITY_DESCRIPTION_LENGTH,
  MAX_ACTIVITY_TITLE_LENGTH,
  ProfileActivityEntryService,
  ACTIVITY_KINDS,
  type ActivityEntry,
} from "../../services/profile-activity.service.js";

const elementName = "dc-profile-activity-editor";

let instanceCount = 0;

const stateLabels: Record<ActivityEntry["state"], string> = {
  Pending: "Waiting to be checked",
  Verified: "Shown on your profile",
  Rejected: "Not accepted",
};

/**
 * Lets the member add a blog post, video or package to their profile by hand. An entry waits for an admin to
 * check it; the member sees where each one stands, and can withdraw it (a shown entry stops showing at once).
 */
@customElement(elementName)
export class ProfileActivityEditorElement extends LitElement {
  @state()
  private _entries: ActivityEntry[] = [];

  @state()
  private _loading = true;

  @state()
  private _saving = false;

  @state()
  private _statusMessage = "";

  @state()
  private _errorMessage = "";

  readonly #idPrefix = `${elementName}-${++instanceCount}`;

  // Render in light DOM so the site's existing styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    this._loading = true;
    try {
      this._entries = await ProfileActivityEntryService.list();
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't load your activity.";
    } finally {
      this._loading = false;
    }
  }

  async #submit(event: SubmitEvent) {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    const value = (name: string) => (form.elements.namedItem(name) as HTMLInputElement | null)?.value.trim() ?? "";

    this._saving = true;
    this._statusMessage = "";
    this._errorMessage = "";
    try {
      const added = await ProfileActivityEntryService.add({
        kind: value("kind"),
        title: value("title"),
        url: value("url"),
        date: value("date"),
        description: value("description"),
      });
      this._entries = [added, ...this._entries];
      this._statusMessage = "Added. We'll check it before it appears on your profile.";
      form.reset();
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't add this. Please try again.";
    } finally {
      this._saving = false;
    }
  }

  async #withdraw(entry: ActivityEntry) {
    this._statusMessage = "";
    this._errorMessage = "";
    try {
      await ProfileActivityEntryService.withdraw(entry.id);
      this._entries = this._entries.filter((e) => e.id !== entry.id);
      this._statusMessage = `“${entry.title}” is withdrawn.`;
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't withdraw this. Please try again.";
    }
  }

  #renderForm(): TemplateResult {
    const id = (name: string) => `${this.#idPrefix}-${name}`;
    return html`
      <form class="dc-profile-activity-editor__form" @submit=${this.#submit}>
        <div class="dc-form-field">
          <label for=${id("kind")}>What is it?</label>
          <select id=${id("kind")} name="kind" required>
            ${ACTIVITY_KINDS.map((kind) => html`<option value=${kind.key}>${kind.label}</option>`)}
          </select>
        </div>
        <div class="dc-form-field">
          <label for=${id("title")}>Title</label>
          <input id=${id("title")} name="title" type="text" required maxlength=${MAX_ACTIVITY_TITLE_LENGTH} />
        </div>
        <div class="dc-form-field">
          <label for=${id("url")}>Link</label>
          <input id=${id("url")} name="url" type="url" required placeholder="https://" aria-describedby=${id("url-hint")} />
          <p id=${id("url-hint")} class="dc-profile-activity-editor__hint">Must start with https://</p>
        </div>
        <div class="dc-form-field">
          <label for=${id("date")}>Date</label>
          <input id=${id("date")} name="date" type="date" required />
        </div>
        <div class="dc-form-field">
          <label for=${id("description")}>Short description (optional)</label>
          <textarea id=${id("description")} name="description" rows="3" maxlength=${MAX_ACTIVITY_DESCRIPTION_LENGTH}></textarea>
        </div>
        <div>
          <button class="btn is-blue" type="submit" ?disabled=${this._saving}>Add for checking</button>
        </div>
      </form>
    `;
  }

  #renderEntry(entry: ActivityEntry): TemplateResult {
    return html`
      <li class="dc-profile-activity-editor__entry" data-entry-id=${entry.id}>
        <a class="dc-profile-activity-editor__entry-title" href=${entry.url} target="_blank" rel="noopener noreferrer">${entry.title}</a>
        <span class="dc-profile-activity-editor__entry-meta">
          ${entry.kindLabel} · ${entry.date} ·
          <span class="dc-profile-activity-editor__state dc-profile-activity-editor__state--${entry.state.toLowerCase()}">${stateLabels[entry.state]}</span>
        </span>
        ${entry.state === "Rejected" && entry.rejectionReason
          ? html`<span class="dc-profile-activity-editor__reason">${entry.rejectionReason}</span>`
          : nothing}
        ${entry.state !== "Rejected"
          ? html`<button class="link-action" type="button" aria-label="Withdraw “${entry.title}”" @click=${() => this.#withdraw(entry)}>
              Withdraw
            </button>`
          : nothing}
      </li>
    `;
  }

  render(): TemplateResult {
    return html`
      <div class="dc-profile-activity-editor">
        ${this.#renderForm()}
        ${this._loading
          ? html`<p class="dc-profile-activity-editor__hint">Loading…</p>`
          : this._entries.length > 0
            ? html`<ul class="dc-profile-activity-editor__entries" aria-label="Activity you added">
                ${this._entries.map((entry) => this.#renderEntry(entry))}
              </ul>`
            : nothing}
        <p class="dc-profile-activity-editor__status" role="status" aria-live="polite">${this._statusMessage}</p>
        ${this._errorMessage
          ? html`<p class="dc-profile-activity-editor__error" role="alert">${this._errorMessage}</p>`
          : nothing}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileActivityEditorElement;
  }
}
