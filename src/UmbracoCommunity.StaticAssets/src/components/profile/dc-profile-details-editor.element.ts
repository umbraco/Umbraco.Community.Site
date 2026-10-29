import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import {
  ProfileOnboardingService,
  MAX_AVATAR_BYTES,
  MAX_AVATAR_MB,
  MAX_BIO_LENGTH,
  MAX_DISPLAY_NAME_LENGTH,
  MAX_HEADLINE_LENGTH,
  type ProfileDetails,
} from "../../services/profile-onboarding.service.js";
import { iconUpload } from "../../svg/lucide-icons.js";

const elementName = "dc-profile-details-editor";

let instanceCount = 0;

type FieldKey = "displayName" | "headline" | "bio";

type FieldValues = Record<FieldKey, string>;

interface FieldConfig {
  key: FieldKey;
  label: string;
  maxLength: number;
  multiline: boolean;
  hint: string;
  save: (value: string) => Promise<ProfileDetails>;
}

const FIELDS: FieldConfig[] = [
  {
    key: "displayName",
    label: "Name",
    maxLength: MAX_DISPLAY_NAME_LENGTH,
    multiline: false,
    hint: "The name shown on your profile. Leave it empty to use your GitHub name.",
    save: (value) => ProfileOnboardingService.updateDisplayName(value),
  },
  {
    key: "headline",
    label: "Headline",
    maxLength: MAX_HEADLINE_LENGTH,
    multiline: false,
    hint: "A short line shown under your name. Leave it empty to use your GitHub company.",
    save: (value) => ProfileOnboardingService.updateHeadline(value),
  },
  {
    key: "bio",
    label: "Bio",
    maxLength: MAX_BIO_LENGTH,
    multiline: true,
    hint: "A few words about you. Leave it empty to use your GitHub bio.",
    save: (value) => ProfileOnboardingService.updateBio(value),
  },
];

/**
 * Edits the member's photo, display name, headline and bio on the Account page. GitHub supplies
 * the defaults: removing the photo shows the GitHub avatar again, and saving an empty name,
 * headline or bio shows the GitHub name, company or bio, which the empty field shows as its placeholder.
 */
@customElement(elementName)
export class ProfileDetailsEditorElement extends LitElement {
  @property({ attribute: "avatar-url" })
  avatarUrl = "";

  @property({ type: Boolean, attribute: "has-custom-avatar" })
  hasCustomAvatar = false;

  /** The member's own display name. Empty when the GitHub name is used. */
  @property({ attribute: "custom-display-name" })
  customDisplayName = "";

  @property({ attribute: "default-display-name" })
  defaultDisplayName = "";

  /** The member's own headline. Empty when the GitHub company is used. */
  @property({ attribute: "custom-headline" })
  customHeadline = "";

  @property({ attribute: "github-headline" })
  gitHubHeadline = "";

  /** The member's own bio. Empty when the GitHub bio is used. */
  @property({ attribute: "custom-bio" })
  customBio = "";

  @property({ attribute: "github-bio" })
  gitHubBio = "";

  /** The member's public profile URL, linked after a successful save. */
  @property({ attribute: "profile-url" })
  profileUrl = "";

  @state()
  private _avatarUrl = "";

  @state()
  private _hasCustomAvatar = false;

  /** The member's own saved values; empty when the GitHub default is used. */
  @state()
  private _saved: FieldValues = { displayName: "", headline: "", bio: "" };

  @state()
  private _defaults: FieldValues = { displayName: "", headline: "", bio: "" };

  @state()
  private _drafts: FieldValues = { displayName: "", headline: "", bio: "" };

  @state()
  private _savingPhoto = false;

  @state()
  private _savingDetails = false;

  /** Announces photo changes, next to the photo controls. */
  @state()
  private _photoStatus = "";

  /** Announces saves of the text fields, next to the Save button. */
  @state()
  private _statusMessage = "";

  /** True after a successful save, until the form is edited again. */
  @state()
  private _showProfileLink = false;

  @state()
  private _photoError = "";

  @state()
  private _detailsError = "";

  readonly #id = `${elementName}-${++instanceCount}`;

  // Render in light DOM so the site's existing form/button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    this._avatarUrl = this.avatarUrl;
    this._hasCustomAvatar = this.hasCustomAvatar;
    this._saved = {
      displayName: this.customDisplayName ?? "",
      headline: this.customHeadline ?? "",
      bio: this.customBio ?? "",
    };
    this._defaults = {
      displayName: this.defaultDisplayName ?? "",
      headline: this.gitHubHeadline ?? "",
      bio: this.gitHubBio ?? "",
    };
    this._drafts = { ...this._saved };
  }

  #applyDetails(details: ProfileDetails) {
    this._saved = {
      displayName: details.hasCustomDisplayName ? details.displayName : "",
      headline: details.hasCustomHeadline ? (details.headline ?? "") : "",
      bio: details.hasCustomBio ? (details.bio ?? "") : "",
    };
    this._defaults = {
      displayName: details.defaultDisplayName ?? "",
      headline: details.gitHubHeadline ?? "",
      bio: details.gitHubBio ?? "",
    };
  }

  #fieldId(key: FieldKey): string {
    return `${this.#id}-${key}`;
  }

  #errorText(error: unknown, fallback: string): string {
    return error instanceof Error && error.message ? error.message : fallback;
  }

  async #onPhotoChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    // Ignore further choices while an upload is in flight. The input is not disabled, so a
    // keyboard user keeps focus on it.
    if (this._savingPhoto) {
      input.value = "";
      return;
    }

    this._photoError = "";
    this._photoStatus = "";

    if (file.size > MAX_AVATAR_BYTES) {
      // Reset so choosing the same file again still fires a change event.
      input.value = "";
      this._photoError = `That image is too large. Please choose one under ${MAX_AVATAR_MB}MB.`;
      return;
    }

    this._savingPhoto = true;
    try {
      const result = await ProfileOnboardingService.uploadAvatar(file);
      this._avatarUrl = result.avatarUrl;
      this._hasCustomAvatar = result.hasCustomAvatar;
      this._photoStatus = "Your photo has been updated.";
    } catch (error) {
      this._photoError = this.#errorText(error, "Couldn't upload that image. Please try again.");
    } finally {
      this._savingPhoto = false;
      input.value = "";
    }
  }

  async #useGitHubPhoto() {
    if (this._savingPhoto) return;

    this._photoError = "";
    this._photoStatus = "";
    this._savingPhoto = true;
    try {
      const result = await ProfileOnboardingService.removeAvatar();
      this._avatarUrl = result.avatarUrl;
      this._hasCustomAvatar = result.hasCustomAvatar;
      this._photoStatus = "Your GitHub photo is used again.";
      // The button that had focus is gone now, so move focus to the upload control.
      await this.updateComplete;
      this.querySelector<HTMLInputElement>(`#${this.#id}-photo`)?.focus();
    } catch (error) {
      this._photoError = this.#errorText(error, "Couldn't remove your photo. Please try again.");
    } finally {
      this._savingPhoto = false;
    }
  }

  #onInput(key: FieldKey, event: Event) {
    const value = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this._drafts = { ...this._drafts, [key]: value };
    // The saved profile no longer matches what's in the form.
    this._showProfileLink = false;
  }

  async #save(event: Event) {
    event.preventDefault();
    if (this._savingDetails) return;

    const trimmed: FieldValues = {
      displayName: this._drafts.displayName.trim(),
      headline: this._drafts.headline.trim(),
      bio: this._drafts.bio.trim(),
    };
    const changed = FIELDS.filter((field) => trimmed[field.key] !== this._saved[field.key]);

    this._detailsError = "";
    this._statusMessage = "";
    this._showProfileLink = false;

    if (changed.length === 0) {
      this._drafts = trimmed;
      this._statusMessage = "There are no changes to save.";
      return;
    }

    this._savingDetails = true;
    try {
      for (const field of changed) {
        this.#applyDetails(await field.save(trimmed[field.key]));
      }
      this._drafts = { ...this._saved };
      this._statusMessage = "Saved.";
      this._showProfileLink = true;
    } catch (error) {
      this._detailsError = this.#errorText(error, "Couldn't save your profile details. Please try again.");
    } finally {
      this._savingDetails = false;
    }
  }

  #renderPhoto(): TemplateResult {
    const inputId = `${this.#id}-photo`;
    const hintId = `${this.#id}-photo-hint`;

    return html`
      <div class="dc-profile-details-editor__photo">
        <img
          class="dc-profile-details-editor__avatar"
          src=${this._avatarUrl}
          alt="Your profile photo"
          width="96"
          height="96"
        />
        <div class="dc-profile-details-editor__photo-controls">
          <div class="dc-profile-details-editor__photo-actions">
            <input
              id=${inputId}
              class="sr-only"
              type="file"
              accept="image/gif,image/jpeg,image/png,image/webp"
              aria-describedby=${hintId}
              aria-busy=${this._savingPhoto ? "true" : "false"}
              @change=${this.#onPhotoChange}
            />
            <label class="btn" for=${inputId}>${iconUpload}<span>Upload photo</span></label>
            ${this._hasCustomAvatar
              ? html`<button class="link-action dc-profile-details-editor__use-github-photo" type="button" @click=${this.#useGitHubPhoto}>
                  Use GitHub photo
                </button>`
              : ""}
          </div>
          <p id=${hintId} class="dc-profile-details-editor__hint">
            GIF, JPEG, PNG, or WEBP, up to ${MAX_AVATAR_MB}MB.
            ${this._hasCustomAvatar ? "" : "You're using your GitHub photo."}
          </p>
          <p class="dc-profile-details-editor__status" role="status" aria-live="polite">${this._photoStatus}</p>
          ${this._photoError
            ? html`<p class="dc-profile-details-editor__error" role="alert">${this._photoError}</p>`
            : ""}
        </div>
      </div>
    `;
  }

  #renderField(field: FieldConfig): TemplateResult {
    const id = this.#fieldId(field.key);
    const draft = this._drafts[field.key];
    const defaultValue = this._defaults[field.key];
    const describedBy = `${id}-hint ${id}-count`;
    const onInput = (event: Event) => this.#onInput(field.key, event);

    return html`
      <div class="dc-form-field">
        <label for=${id}>${field.label}</label>
        ${field.multiline
          ? html`<textarea
              id=${id}
              rows="4"
              maxlength=${field.maxLength}
              placeholder=${defaultValue}
              aria-describedby=${describedBy}
              .value=${draft}
              @input=${onInput}
            ></textarea>`
          : html`<input
              id=${id}
              type="text"
              maxlength=${field.maxLength}
              placeholder=${defaultValue}
              aria-describedby=${describedBy}
              .value=${draft}
              @input=${onInput}
            />`}
        <div class="dc-profile-details-editor__field-footer">
          <p id="${id}-hint" class="dc-profile-details-editor__hint">${field.hint}</p>
          <p
            id="${id}-count"
            class="dc-profile-details-editor__count ${draft.length >= field.maxLength
              ? "dc-profile-details-editor__count--limit"
              : ""}"
          >
            ${draft.length}/${field.maxLength}
          </p>
        </div>
      </div>
    `;
  }

  #renderForm(): TemplateResult {
    return html`
      <form class="dc-profile-details-editor__form" @submit=${this.#save}>
        ${FIELDS.map((field) => this.#renderField(field))}
        <div class="dc-profile-details-editor__actions">
          <button class="btn is-blue" type="submit" ?disabled=${this._savingDetails}>Save</button>
          <p class="dc-profile-details-editor__save-status" role="status" aria-live="polite">
            ${this._statusMessage}
            ${this._showProfileLink && this.profileUrl
              ? html`<a
                  class="dc-profile-details-editor__profile-link no-link-animation"
                  href=${this.profileUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  >View your profile</a
                >`
              : ""}
          </p>
        </div>
        ${this._detailsError
          ? html`<p class="dc-profile-details-editor__error" role="alert">${this._detailsError}</p>`
          : ""}
      </form>
    `;
  }

  render(): TemplateResult {
    return html`
      <div class="dc-profile-details-editor">
        ${this.#renderPhoto()} ${this.#renderForm()}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileDetailsEditorElement;
  }
}
