import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { ProfileOnboardingService, MAX_BIO_LENGTH } from "../../services/profile-onboarding.service.js";
import { iconPenLine } from "../../svg/lucide-icons.js";

const elementName = "dc-profile-bio-editor";

type Phase = "display" | "editing" | "saving" | "error";

/**
 * Lets the profile owner edit their bio inline on their own Community Profile page — an
 * edit badge next to the bio text reveals a textarea in place, rather than sending them back
 * to the onboarding wizard. Only rendered by the view when Model.IsOwnProfile is true.
 */
@customElement(elementName)
export class ProfileBioEditorElement extends LitElement {
  @property({ attribute: "bio" })
  bio = "";

  @state()
  private _bio = "";

  @state()
  private _draft = "";

  @state()
  private _phase: Phase = "display";

  @state()
  private _errorMessage = "";

  // Render in light DOM so the site's existing form/button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    this._bio = this.bio;
  }

  #startEditing() {
    this._draft = this._bio;
    this._phase = "editing";
  }

  #cancelEditing() {
    this._phase = "display";
  }

  async #save(event: Event) {
    event.preventDefault();
    this._phase = "saving";
    try {
      await ProfileOnboardingService.updateBio(this._draft);
      this._bio = this._draft;
      this._phase = "display";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't save your bio. Please try again.";
      this._phase = "error";
    }
  }

  #renderDisplay(): TemplateResult {
    return html`
      <div class="community-profile__bio-editor">
        ${this._bio
          ? html`<p class="community-profile__bio">${this._bio}</p>`
          : html`<p class="community-profile__bio community-profile__bio--empty">
              Add a bio to tell the community about yourself.
            </p>`}
        <button
          class="community-profile__bio-edit"
          type="button"
          title="Edit bio"
          aria-label="Edit bio"
          @click=${this.#startEditing}
        >
          ${iconPenLine}
        </button>
      </div>
    `;
  }

  #renderEditing(): TemplateResult {
    return html`
      <form class="community-profile__bio-editor community-profile__bio-editor--editing dc-form-field" @submit=${this.#save}>
        <textarea
          rows="4"
          maxlength="${MAX_BIO_LENGTH}"
          .value=${this._draft}
          @input=${(event: Event) => (this._draft = (event.target as HTMLTextAreaElement).value)}
        ></textarea>
        <div class="community-profile__bio-editor-footer">
          <p class="community-profile__bio-count">${this._draft.length}/${MAX_BIO_LENGTH}</p>
          <div class="community-profile__bio-actions">
            <button class="btn is-transparent" type="button" @click=${this.#cancelEditing}>Cancel</button>
            <button class="btn is-white" type="submit" ?disabled=${this._phase === "saving"}>Save</button>
          </div>
        </div>
        ${this._phase === "error"
          ? html`<p class="community-profile__bio-error" role="alert">${this._errorMessage}</p>`
          : ""}
      </form>
    `;
  }

  render(): TemplateResult {
    return this._phase === "display" ? this.#renderDisplay() : this.#renderEditing();
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileBioEditorElement;
  }
}
