import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { ProfileOnboardingService } from "../../services/profile-onboarding.service.js";

const elementName = "dc-profile-hidden-banner";

/**
 * Banner at the top of the owner's own profile page while the profile is still hidden. Its
 * button shows the profile and reloads the page. Only rendered by the view when
 * Model.IsHiddenPreview is true.
 */
@customElement(elementName)
export class ProfileHiddenBannerElement extends LitElement {
  /** The account page URL, where visibility can be changed later. Optional. */
  @property({ attribute: "account-url" })
  accountUrl = "";

  @state()
  private _saving = false;

  @state()
  private _errorMessage = "";

  // Render in light DOM so the site's existing button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  /** Reloads the page so it renders as the now visible profile. */
  reloadPage() {
    window.location.reload();
  }

  async #showProfile() {
    this._saving = true;
    this._errorMessage = "";
    try {
      await ProfileOnboardingService.setProfileVisible(true);
      this.reloadPage();
    } catch (error) {
      this._errorMessage =
        error instanceof Error && error.message
          ? error.message
          : "Couldn't show your profile. Please try again.";
      this._saving = false;
    }
  }

  #renderNote(): TemplateResult {
    return this.accountUrl
      ? html`<p class="dc-profile-hidden-banner__note">
          You can change this later from your <a href=${this.accountUrl}>account page</a>.
        </p>`
      : html`<p class="dc-profile-hidden-banner__note">You can change this later from your account page.</p>`;
  }

  render(): TemplateResult {
    return html`
      <section class="dc-profile-hidden-banner" aria-label="Profile visibility">
        <div class="dc-profile-hidden-banner__inner">
          <p class="dc-profile-hidden-banner__message">This profile is currently hidden. Only you can see it. If you show it, it becomes public and anyone in the world can see it, not just you.</p>
          ${this.#renderNote()}
          <button
            class="btn is-blue dc-profile-hidden-banner__action"
            type="button"
            ?disabled=${this._saving}
            @click=${this.#showProfile}
          >
            Show my profile
          </button>
          ${this._errorMessage
            ? html`<p class="dc-profile-hidden-banner__error" role="alert">${this._errorMessage}</p>`
            : ""}
        </div>
      </section>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileHiddenBannerElement;
  }
}
