import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import {
  ACTIVITY_IMAGE_TYPES,
  MAX_ACTIVITY_DESCRIPTION_LENGTH,
  MAX_ACTIVITY_IMAGE_MB,
  MAX_ACTIVITY_TITLE_LENGTH,
  ProfileActivityEntryService,
  ACTIVITY_KINDS,
  activityImageError,
  isLookupLink,
  type ActivityEntry,
  type ActivityLinkDetails,
} from "../../services/profile-activity.service.js";

import { iconPencil, iconX } from "../../svg/lucide-icons.js";
import { renderProfileTimelineEntry } from "./profile-timeline-entry.js";

const elementName = "dc-profile-activity-editor";

/** The timeline icon for a kind. Keep in sync with ProfileActivityEntry.Group and ProfileTimeline.IconKey. */
const kindIconKeys: Record<string, string> = {
  blog: "blog",
  videos: "video",
  packages: "package",
  event: "meetup",
};

let instanceCount = 0;

const stateLabels: Record<ActivityEntry["state"], string> = {
  // From before entries went on the profile at once: still waiting for a moderator.
  Pending: "Waiting for a moderator",
  Verified: "On your profile",
  Rejected: "Removed by a moderator",
};

/** The text fields the page's details can fill in. */
type FillableField = "title" | "date" | "description";

/**
 * Lets the member add activity to their profile by hand: it goes on the profile at once (a moderator can remove it
 * afterwards). The link comes first: once it is a valid https address (on leaving the field, pasting, Enter or "Fetch
 * details") the server reads the page and the form is filled in from it, only where the member hasn't typed, so pasting
 * a link is often enough. The page's picture, which the timeline card uses when the member attaches none, is shown too
 * (our copy, served from this site). The member's entries are listed with where each one stands, and can be edited in
 * the same form or withdrawn. An entry can carry a picture of the member's own, checked (type and size) and previewed
 * here before it is sent.
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

  /** The picture chosen for the entry, and its preview address (an object URL, revoked when it goes). */
  @state()
  private _image: File | null = null;

  @state()
  private _imagePreviewUrl = "";

  @state()
  private _imageError = "";

  /** The entry being edited in the form, or null when the form adds a new one. */
  @state()
  private _editing: ActivityEntry | null = null;

  /** While editing: the member removed the entry's current picture. */
  @state()
  private _removeCurrentImage = false;

  @state()
  private _lookingUp = false;

  @state()
  private _lookupMessage = "";

  /** Our copy of the linked page's picture, used for the card when the member attaches none. */
  @state()
  private _pagePictureUrl: string | null = null;

  readonly #idPrefix = `${elementName}-${++instanceCount}`;

  /** The link last looked up, so leaving the field again doesn't read the same page twice. */
  #lookedUpUrl = "";

  /** Counts lookups, so an answer that arrives after a newer one started is ignored. */
  #lookupCount = 0;

  /** What a lookup put in each field: a field still holding it wasn't typed by the member and may be filled again. */
  #filled = new Map<FillableField, string>();

  /** The member chose the kind themselves (or is editing), so a lookup leaves it alone. */
  #kindChosen = false;

  // Render in light DOM so the site's existing styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#setImage(null);
  }

  #id(name: string): string {
    return `${this.#idPrefix}-${name}`;
  }

  #form(): HTMLFormElement | null {
    return this.querySelector<HTMLFormElement>("form.dc-profile-activity-editor__form");
  }

  #field(name: string): HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement | null {
    return (this.#form()?.elements.namedItem(name) as HTMLInputElement | null) ?? null;
  }

  /** Keeps the chosen picture and its preview in step, releasing the previous preview. */
  #setImage(file: File | null) {
    if (this._imagePreviewUrl) {
      URL.revokeObjectURL(this._imagePreviewUrl);
    }
    this._image = file;
    this._imagePreviewUrl = file ? URL.createObjectURL(file) : "";
  }

  #imageInput(): HTMLInputElement | null {
    return this.querySelector<HTMLInputElement>('input[name="image"]');
  }

  #chooseImage(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    const error = file ? activityImageError(file) : null;
    this._imageError = error ?? "";
    if (error) {
      // Nothing is attached, so what was refused isn't sent by accident.
      input.value = "";
      this.#setImage(null);
      return;
    }
    this.#setImage(file);
  }

  /** Drops the picture the member chose (back to the entry's current picture, or the page's, if any). */
  #removeImage() {
    const input = this.#imageInput();
    if (input) {
      input.value = "";
    }
    this._imageError = "";
    this.#setImage(null);
    input?.focus();
  }

  /** While editing: removes the picture the entry has now. */
  #removeCurrentImage() {
    this._removeCurrentImage = true;
    this.#imageInput()?.focus();
  }

  /** Forgets the form's lookup and picture state, for a fresh add or a new edit. */
  #resetFormState() {
    this._imageError = "";
    this.#setImage(null);
    this._removeCurrentImage = false;
    this._pagePictureUrl = null;
    this._lookupMessage = "";
    this._lookingUp = false;
    this.#lookupCount++;
    this.#lookedUpUrl = "";
    this.#filled.clear();
    this.#kindChosen = false;
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

  // ---- looking up the link ----

  #linkValue(): string {
    return this.#field("url")?.value.trim() ?? "";
  }

  /** Looks the link up on its own (leaving the field, pasting, Enter), only when adding and only once per link. */
  #autoLookUp() {
    if (this._editing) return;
    const url = this.#linkValue();
    if (!isLookupLink(url) || url === this.#lookedUpUrl) return;
    void this.#lookUp(url);
  }

  #onLinkInput() {
    // A changed link no longer has the page's picture.
    if (this.#linkValue() !== this.#lookedUpUrl) {
      this._pagePictureUrl = null;
    }
  }

  #onLinkPaste() {
    // The pasted text is in the field only after the paste event.
    setTimeout(() => this.#autoLookUp(), 0);
  }

  #onLinkKeydown(event: KeyboardEvent) {
    if (event.key !== "Enter" || this._editing || !isLookupLink(this.#linkValue())) return;
    // Enter in the link field fetches the page's details rather than adding a half-filled entry.
    event.preventDefault();
    if (this.#linkValue() !== this.#lookedUpUrl) {
      void this.#lookUp(this.#linkValue());
    }
  }

  #fetchDetails() {
    const url = this.#linkValue();
    if (!isLookupLink(url)) {
      this._lookupMessage = "Enter a link that starts with https:// first.";
      return;
    }
    void this.#lookUp(url);
  }

  async #lookUp(url: string) {
    const lookup = ++this.#lookupCount;
    this.#lookedUpUrl = url;
    this._lookingUp = true;
    this._lookupMessage = "Looking up the page…";
    const details = await ProfileActivityEntryService.lookUp(url);
    // A newer lookup has taken over (and owns the status).
    if (lookup !== this.#lookupCount) return;
    this._lookingUp = false;
    if (this.#linkValue() !== url) {
      // The member changed the link meanwhile: this page's details no longer apply.
      this._lookupMessage = "";
      this.#lookedUpUrl = "";
      return;
    }
    this._pagePictureUrl = details.imageUrl;
    this._lookupMessage = this.#fill(details)
      ? `Filled in from the page. Check the details and ${this._editing ? "save" : "add"} it.`
      : "";
  }

  /** Fills in what the page offers, only in fields the member hasn't typed in. Returns true when anything was. */
  #fill(details: ActivityLinkDetails): boolean {
    let filled = false;
    const values: Record<FillableField, string | null> = {
      title: details.title,
      date: details.date,
      description: details.description,
    };
    for (const name of Object.keys(values) as FillableField[]) {
      const value = values[name];
      const field = this.#field(name);
      if (!value || !field) continue;
      const current = field.value;
      if (current.trim() === "" || current === this.#filled.get(name)) {
        field.value = value;
        this.#filled.set(name, value);
        filled = true;
      }
    }
    const kind = this.#field("kind");
    if (details.kind && kind && !this.#kindChosen && ACTIVITY_KINDS.some((k) => k.key === details.kind)) {
      kind.value = details.kind;
      filled = true;
    }
    return filled || !!details.imageUrl;
  }

  // ---- adding and editing ----

  async #submit(event: SubmitEvent) {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    const value = (name: string) => (form.elements.namedItem(name) as HTMLInputElement | null)?.value.trim() ?? "";
    const fields = {
      kind: value("kind"),
      title: value("title"),
      url: value("url"),
      date: value("date"),
      description: value("description"),
      image: this._image,
    };

    this._saving = true;
    this._statusMessage = "";
    this._errorMessage = "";
    const editing = this._editing;
    try {
      if (editing) {
        const updated = await ProfileActivityEntryService.update(editing.id, {
          ...fields,
          removeImage: this._removeCurrentImage,
        });
        this._entries = this._entries.map((e) => (e.id === updated.id ? updated : e));
        this.#stopEditing(form);
        this._statusMessage = `“${updated.title}” is updated.`;
        await this.updateComplete;
        this.#focusEditButton(updated.id);
      } else {
        const added = await ProfileActivityEntryService.add(fields);
        this._entries = [added, ...this._entries];
        this._statusMessage = `“${added.title}” is on your profile now.`;
        form.reset();
        this.#resetFormState();
      }
    } catch (error) {
      const fallback = editing ? "Couldn't save your changes. Please try again." : "Couldn't add this. Please try again.";
      this._errorMessage = error instanceof Error && error.message ? error.message : fallback;
    } finally {
      this._saving = false;
    }
  }

  async #startEdit(entry: ActivityEntry) {
    this.#resetFormState();
    this.#kindChosen = true;
    this._editing = entry;
    this._statusMessage = "";
    this._errorMessage = "";
    await this.updateComplete;

    const set = (name: string, value: string | null) => {
      const field = this.#field(name);
      if (field) field.value = value ?? "";
    };
    set("url", entry.url);
    set("kind", entry.kind);
    set("title", entry.title);
    set("date", entry.date);
    set("description", entry.description);
    this.#lookedUpUrl = entry.url;

    const heading = this.querySelector<HTMLElement>(".dc-profile-activity-editor__form-heading");
    heading?.scrollIntoView?.({ block: "start", behavior: "smooth" });
    heading?.focus();
  }

  #stopEditing(form: HTMLFormElement | null = this.#form()) {
    this._editing = null;
    form?.reset();
    this.#resetFormState();
  }

  async #cancelEdit() {
    const id = this._editing?.id;
    this.#stopEditing();
    this._statusMessage = "";
    this._errorMessage = "";
    await this.updateComplete;
    if (id !== undefined) this.#focusEditButton(id);
  }

  #focusEditButton(id: number) {
    this.querySelector<HTMLButtonElement>(`button[data-edit-id="${id}"]`)?.focus();
  }

  async #withdraw(entry: ActivityEntry) {
    this._statusMessage = "";
    this._errorMessage = "";
    try {
      await ProfileActivityEntryService.withdraw(entry.id);
      this._entries = this._entries.filter((e) => e.id !== entry.id);
      if (this._editing?.id === entry.id) {
        this.#stopEditing();
      }
      this._statusMessage = `“${entry.title}” is withdrawn.`;
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't withdraw this. Please try again.";
    }
  }

  // ---- rendering ----

  /** The picture part of the form: the member's own (chosen now, or the entry's current one), else the page's. */
  #renderPicture(): TemplateResult {
    const id = (name: string) => this.#id(name);
    const current = this._editing?.imageUrl && !this._removeCurrentImage ? this._editing.imageUrl : null;
    const pagePicture = !this._imagePreviewUrl && !current ? this._pagePictureUrl : null;
    const label = current
      ? "Replace picture (optional)"
      : pagePicture
        ? "Use a different picture (optional)"
        : "Picture (optional)";

    let preview: TemplateResult | typeof nothing = nothing;
    if (this._imagePreviewUrl) {
      preview = html`<div class="dc-profile-activity-editor__picture-preview">
        <img src=${this._imagePreviewUrl} alt="Preview of the picture you chose" />
        <button class="link-action" type="button" @click=${this.#removeImage}>Remove picture</button>
      </div>`;
    } else if (current) {
      preview = html`<div class="dc-profile-activity-editor__picture-preview">
        <img src=${current} alt="The picture on this entry now" />
        <button class="link-action" type="button" @click=${this.#removeCurrentImage}>Remove picture</button>
      </div>`;
    } else if (pagePicture) {
      preview = html`<div class="dc-profile-activity-editor__picture-preview dc-profile-activity-editor__page-picture">
        <img src=${pagePicture} alt="The picture from the linked page" />
        <p class="dc-profile-activity-editor__hint">This picture from the page will be used.</p>
      </div>`;
    }

    return html`
      <div class="dc-form-field dc-profile-activity-editor__picture">
        <label for=${id("image")}>${label}</label>
        <input
          id=${id("image")}
          name="image"
          type="file"
          accept=${ACTIVITY_IMAGE_TYPES.join(",")}
          aria-describedby=${this._imageError ? `${id("image-hint")} ${id("image-error")}` : id("image-hint")}
          aria-invalid=${this._imageError ? "true" : "false"}
          @change=${this.#chooseImage}
        />
        <p id=${id("image-hint")} class="dc-profile-activity-editor__hint">
          JPEG, PNG, GIF or WebP, up to ${MAX_ACTIVITY_IMAGE_MB} MB. Shown on the card in your timeline.
        </p>
        ${this._imageError
          ? html`<p id=${id("image-error")} class="dc-profile-activity-editor__error" role="alert">${this._imageError}</p>`
          : nothing}
        ${preview}
      </div>
    `;
  }

  #renderForm(): TemplateResult {
    const id = (name: string) => this.#id(name);
    const editing = this._editing;
    return html`
      ${editing
        ? html`<h3 id=${id("form-heading")} class="dc-profile-display-editor__heading dc-profile-activity-editor__form-heading" tabindex="-1">
            Edit activity
          </h3>`
        : nothing}
      <form
        class="dc-profile-activity-editor__form"
        aria-label=${editing ? nothing : "Add activity"}
        aria-labelledby=${editing ? id("form-heading") : nothing}
        @submit=${this.#submit}
      >
        <div class="dc-form-field">
          <label for=${id("url")}>Link</label>
          <div class="dc-profile-activity-editor__link">
            <input
              id=${id("url")}
              name="url"
              type="url"
              required
              placeholder="https://"
              aria-describedby="${id("url-hint")} ${id("lookup-status")}"
              @change=${this.#autoLookUp}
              @input=${this.#onLinkInput}
              @paste=${this.#onLinkPaste}
              @keydown=${this.#onLinkKeydown}
            />
            <button class="btn" type="button" ?disabled=${this._lookingUp} @click=${this.#fetchDetails}>
              Fetch details
            </button>
          </div>
          <p id=${id("url-hint")} class="dc-profile-activity-editor__hint">
            Must start with https://. ${editing ? "" : "We fill in what we can from the page."}
          </p>
          <p id=${id("lookup-status")} class="dc-profile-activity-editor__hint" role="status" aria-live="polite">
            ${this._lookupMessage}
          </p>
        </div>
        <div class="dc-form-field">
          <label for=${id("kind")}>What is it?</label>
          <select id=${id("kind")} name="kind" required @change=${() => (this.#kindChosen = true)}>
            ${ACTIVITY_KINDS.map((kind) => html`<option value=${kind.key}>${kind.label}</option>`)}
          </select>
        </div>
        <div class="dc-form-field">
          <label for=${id("title")}>Title</label>
          <input id=${id("title")} name="title" type="text" required maxlength=${MAX_ACTIVITY_TITLE_LENGTH} />
        </div>
        <div class="dc-form-field">
          <label for=${id("date")}>Date</label>
          <input id=${id("date")} name="date" type="date" required />
        </div>
        <div class="dc-form-field">
          <label for=${id("description")}>Short description (optional)</label>
          <textarea id=${id("description")} name="description" rows="3" maxlength=${MAX_ACTIVITY_DESCRIPTION_LENGTH}></textarea>
        </div>
        ${this.#renderPicture()}
        <div class="dc-profile-activity-editor__form-actions">
          <button class="btn is-blue" type="submit" ?disabled=${this._saving}>
            ${editing ? "Save changes" : "Add to my profile"}
          </button>
          ${editing
            ? html`<button class="btn" type="button" @click=${this.#cancelEdit}>Cancel</button>`
            : nothing}
        </div>
      </form>
    `;
  }

  /** An entry drawn as it looks in the profile timeline, with where it stands and ways to edit or withdraw it. */
  #renderEntry(entry: ActivityEntry): TemplateResult {
    const extra = html`
      <div class="dc-profile-activity-editor__entry-tools">
        <span class="dc-profile-activity-editor__state dc-profile-activity-editor__state--${entry.state.toLowerCase()}"
          >${stateLabels[entry.state]}</span
        >
        ${entry.state === "Rejected" && entry.rejectionReason
          ? html`<span class="dc-profile-activity-editor__reason">${entry.rejectionReason}</span>`
          : nothing}
        ${entry.state !== "Rejected"
          ? html`<button
              class="dc-profile-display-editor__action"
              type="button"
              data-edit-id=${entry.id}
              aria-label="Edit “${entry.title}”"
              @click=${() => this.#startEdit(entry)}
            >
              ${iconPencil} Edit
            </button>`
          : nothing}
        <button
          class="dc-profile-display-editor__action"
          type="button"
          data-withdraw-id=${entry.id}
          aria-label="Withdraw “${entry.title}”"
          @click=${() => this.#withdraw(entry)}
        >
          ${iconX} Withdraw
        </button>
      </div>
    `;
    return renderProfileTimelineEntry(
      {
        title: entry.title,
        iconKey: kindIconKeys[entry.kind] ?? "other",
        typeLabel: entry.kindLabel,
        url: entry.url,
        date: entry.date,
        meta: entry.kindLabel,
        card: this.#cardFor(entry),
      },
      {
        extra,
        itemId: String(entry.id),
        classes: { "dc-profile-activity-editor__entry": true, "is-editing": this._editing?.id === entry.id },
      }
    );
  }

  /** The entry's card as the timeline shows it: the member's own picture, else our copy of the page's. */
  #cardFor(entry: ActivityEntry) {
    const imageUrl = entry.imageUrl ?? entry.pageImageUrl;
    return imageUrl ? { imageUrl, title: entry.title, description: entry.description } : null;
  }

  render(): TemplateResult {
    return html`
      <div class="dc-profile-activity-editor">
        ${this.#renderForm()}
        ${this._loading
          ? html`<p class="dc-profile-activity-editor__hint">Loading…</p>`
          : this._entries.length > 0
            ? html`<h3 class="dc-profile-display-editor__heading">Activity you added</h3>
                <ol class="community-profile__timeline-list dc-profile-activity-editor__entries" aria-label="Activity you added">
                  ${this._entries.map((entry) => this.#renderEntry(entry))}
                </ol>`
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
