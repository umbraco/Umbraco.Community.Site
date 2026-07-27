import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { ProfileOnboardingService, MAX_AVATAR_BYTES, MAX_AVATAR_MB } from "../../services/profile-onboarding.service.js";
import { iconUpload } from "../../svg/lucide-icons.js";

const elementName = "dc-profile-avatar-editor";

type Phase = "idle" | "saving" | "error";

/**
 * Lets the profile owner replace their avatar directly from their own Community Profile
 * page — a small upload badge overlaid on the corner of the avatar they already see, rather
 * than sending them back to the onboarding wizard for something this quick. Only rendered
 * by the view when Model.IsOwnProfile is true; other visitors just get the plain <img>.
 */
@customElement(elementName)
export class ProfileAvatarEditorElement extends LitElement {
  @property({ attribute: "avatar-url" })
  avatarUrl = "";

  @property({ attribute: "display-name" })
  displayName = "";

  @state()
  private _avatarUrl = "";

  @state()
  private _phase: Phase = "idle";

  @state()
  private _errorMessage = "";

  // Render in light DOM so the site's existing form/button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    this._avatarUrl = this.avatarUrl;
  }

  async #handleChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (file.size > MAX_AVATAR_BYTES) {
      input.value = "";
      this._errorMessage = `That image is too large — please choose one under ${MAX_AVATAR_MB}MB.`;
      this._phase = "error";
      return;
    }

    this._phase = "saving";
    try {
      const result = await ProfileOnboardingService.uploadAvatar(file);
      this._avatarUrl = result.avatarUrl;
      this._phase = "idle";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't upload that image. Please try again.";
      this._phase = "error";
    } finally {
      input.value = "";
    }
  }

  render(): TemplateResult {
    return html`
      <div class="community-profile__avatar-editor">
        <img
          class="community-profile__avatar"
          src=${this._avatarUrl}
          alt=${this.displayName}
          width="170"
          height="170"
          loading="eager"
        />
        <label
          class="community-profile__avatar-upload ${this._phase === "saving" ? "is-saving" : ""}"
          for="dc-profile-avatar-input"
          title="Upload a new photo"
          aria-label="Upload a new profile photo"
        >
          ${iconUpload}
        </label>
        <input
          id="dc-profile-avatar-input"
          type="file"
          accept="image/gif,image/jpeg,image/png,image/webp"
          class="sr-only"
          ?disabled=${this._phase === "saving"}
          @change=${this.#handleChange}
        />
        ${this._phase === "error"
          ? html`<p class="community-profile__avatar-error" role="alert">${this._errorMessage}</p>`
          : ""}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileAvatarEditorElement;
  }
}
