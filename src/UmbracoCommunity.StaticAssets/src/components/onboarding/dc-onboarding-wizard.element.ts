import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { ifDefined } from "lit/directives/if-defined.js";
import {
  ProfileOnboardingService,
  MAX_AVATAR_BYTES,
  MAX_AVATAR_MB,
  MAX_BIO_LENGTH,
  type OnboardingState,
} from "../../services/profile-onboarding.service.js";
import { iconUpload, iconCopy, iconCheck } from "../../svg/lucide-icons.js";
import { copyToClipboard } from "../../util/clipboard.js";

const elementName = "dc-onboarding-wizard";

type Phase = "loading" | "ready" | "saving" | "error" | "done";
type Step = 1 | 2;

const TOTAL_STEPS: Step = 2;

@customElement(elementName)
export class OnboardingWizardElement extends LitElement {
  /** The tenant's Community Profile page URL (no handle segment) — set by the server. */
  @property({ attribute: "profile-base-url" })
  profileBaseUrl = "";

  /** Editor-configured "want to change your handle?" link (OnboardingPage.DevRelContactForm). */
  @property({ attribute: "devrel-contact-url" })
  devrelContactUrl = "";

  @property({ attribute: "devrel-contact-target" })
  devrelContactTarget = "";

  /** Editor-set title for the DevRel contact link — falls back to a default label if unset. */
  @property({ attribute: "devrel-contact-title" })
  devrelContactTitle = "";

  @state()
  private _phase: Phase = "loading";

  @state()
  private _step: Step = 1;

  @state()
  private _onboardingState?: OnboardingState;

  @state()
  private _bioDraft = "";

  @state()
  private _errorMessage = "";

  @state()
  private _showCopiedFeedback = false;

  // Whatever action most recently failed — "Try again" re-runs exactly that, rather than
  // always restarting from the beginning (which would just reload the same draft state and
  // strand the member on whichever step they were on when the retry loop began).
  #retryAction: (() => void) | null = null;

  #copiedTimeout?: ReturnType<typeof setTimeout>;

  // Render in light DOM so the site's existing form/button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    void this.#start();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this.#copiedTimeout) {
      clearTimeout(this.#copiedTimeout);
    }
  }

  async #start() {
    this._phase = "loading";
    try {
      const state = await ProfileOnboardingService.start();
      this._onboardingState = state;
      this._bioDraft = state.bio ?? "";
      this._phase = "ready";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Something went wrong. Please try again.";
      this.#retryAction = () => this.#start();
      this._phase = "error";
    }
  }

  #next() {
    if (this._step < TOTAL_STEPS) {
      this._step = (this._step + 1) as Step;
    }
  }

  #back() {
    if (this._step > 1) {
      this._step = (this._step - 1) as Step;
    }
  }

  async #handleAvatarChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (file.size > MAX_AVATAR_BYTES) {
      // Reset so choosing the same file again still fires a change event.
      input.value = "";
      this._errorMessage = `That image is too large — please choose one under ${MAX_AVATAR_MB}MB.`;
      // Nothing to retry against; "Try again" just returns to the form so they can pick another file.
      this.#retryAction = () => {
        this._phase = "ready";
      };
      this._phase = "error";
      return;
    }

    await this.#uploadAvatar(file);
  }

  async #uploadAvatar(file: File) {
    this._phase = "saving";
    try {
      const result = await ProfileOnboardingService.uploadAvatar(file);
      this._onboardingState = this._onboardingState
        ? { ...this._onboardingState, avatarUrl: result.avatarUrl, hasCustomAvatar: result.hasCustomAvatar }
        : this._onboardingState;
      this._phase = "ready";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't upload that image. Please try again.";
      this.#retryAction = () => this.#uploadAvatar(file);
      this._phase = "error";
    }
  }

  async #handleRemoveAvatar() {
    this._phase = "saving";
    try {
      const result = await ProfileOnboardingService.removeAvatar();
      this._onboardingState = this._onboardingState
        ? { ...this._onboardingState, avatarUrl: result.avatarUrl, hasCustomAvatar: result.hasCustomAvatar }
        : this._onboardingState;
      this._phase = "ready";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't remove that image. Please try again.";
      this.#retryAction = () => this.#handleRemoveAvatar();
      this._phase = "error";
    }
  }

  async #handleDetailsSubmit(event: Event) {
    event.preventDefault();
    await this.#saveBioAndAdvance();
  }

  async #saveBioAndAdvance() {
    this._phase = "saving";
    try {
      await ProfileOnboardingService.updateBio(this._bioDraft);
      this._phase = "ready";
      this.#next();
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't save your bio. Please try again.";
      this.#retryAction = () => this.#saveBioAndAdvance();
      this._phase = "error";
    }
  }

  async #finish() {
    this._phase = "saving";
    try {
      await ProfileOnboardingService.complete();
      this._phase = "done";
      const returnUrl = this.#getSafeReturnUrl();
      if (returnUrl) {
        window.location.href = returnUrl;
        return;
      }
      const handle = this._onboardingState?.handle ?? "";
      if (this.profileBaseUrl && handle) {
        window.location.href = `${this.profileBaseUrl.replace(/\/$/, "")}/${handle}`;
      }
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't finish onboarding. Please try again.";
      this.#retryAction = () => this.#finish();
      this._phase = "error";
    }
  }

  /**
   * The redirect middleware appends ?returnUrl= so onboarding can send the member back to
   * wherever they originally landed. Only same-origin relative paths are honoured — a
   * query param is caller-controlled, so an absolute or protocol-relative value is rejected
   * outright rather than risking an open redirect.
   */
  #getSafeReturnUrl(): string | null {
    const returnUrl = new URLSearchParams(window.location.search).get("returnUrl");
    if (!returnUrl || !returnUrl.startsWith("/") || returnUrl.startsWith("//")) {
      return null;
    }
    return returnUrl;
  }

  #renderStepIndicator(): TemplateResult {
    return html`<p class="dc-onboarding__step-indicator">Step ${this._step} of ${TOTAL_STEPS}</p>`;
  }

  // Falls back to the DevRel contact page if the editor hasn't set OnboardingPage.DevRelContactForm yet.
  #devrelContactHref(): string {
    return this.devrelContactUrl || "/connect/the-developer-relations-team/contact-devrel/";
  }

  // Honours the link's own title if the editor set one, rather than a hardcoded label.
  #devrelContactLabel(): string {
    return this.devrelContactTitle || "Email the DevRel team";
  }

  // Trimmed once here since both the display and the copy handler need the same base.
  #profileBaseUrl(): string | null {
    return this.profileBaseUrl ? this.profileBaseUrl.replace(/\/$/, "") : null;
  }

  /**
   * Shows the actual link the member's profile will live at (scheme + host included, so it's
   * obviously "https://localhost:xxxx/..." in dev vs the real domain elsewhere) rather than
   * just their handle. Falls back to "@handle" if the tenant hasn't set up a Community
   * Profile page yet, since there's no base URL to build a real link from.
   */
  #renderProfileUrl(handle: string): TemplateResult {
    const base = this.#profileBaseUrl();
    if (!base) {
      return html`<span class="dc-onboarding__handle-slug">@${handle}</span>`;
    }

    return html`<span class="dc-onboarding__handle-base">${base}/</span><span
        class="dc-onboarding__handle-slug"
        >${handle}</span
      >`;
  }

  async #handleCopyProfileUrl(handle: string) {
    const base = this.#profileBaseUrl();
    if (!base) return;

    const copied = await copyToClipboard(`${base}/${handle}`);
    if (!copied) return;

    this._showCopiedFeedback = true;
    if (this.#copiedTimeout) {
      clearTimeout(this.#copiedTimeout);
    }
    this.#copiedTimeout = setTimeout(() => {
      this._showCopiedFeedback = false;
    }, 2000);
  }

  #renderStepDetails(state: OnboardingState): TemplateResult {
    const target = this.devrelContactTarget || undefined;
    const rel = target === "_blank" ? "noopener noreferrer" : undefined;
    return html`
      <section class="dc-onboarding__step">
        ${this.#renderStepIndicator()}
        <h2>Your details</h2>
        <p class="dc-onboarding__intro">This is how you'll show up across the community.</p>

        <div class="dc-onboarding__identity">
          <div class="dc-onboarding__avatar-block">
            <img class="dc-onboarding__avatar-preview" src=${state.avatarUrl} alt="" width="220" height="220" />
            <div class="dc-onboarding__avatar-actions">
              <label class="btn" for="dc-onboarding-avatar-input">${iconUpload}<span>Upload image</span></label>
              <input
                id="dc-onboarding-avatar-input"
                type="file"
                accept="image/gif,image/jpeg,image/png,image/webp"
                class="sr-only"
                @change=${this.#handleAvatarChange}
              />
              ${state.hasCustomAvatar
                ? html`<button class="dc-onboarding__avatar-remove" type="button" @click=${this.#handleRemoveAvatar}>
                    Remove
                  </button>`
                : ""}
            </div>
            <p class="dc-onboarding__avatar-hint">GIF, JPEG, PNG, or WEBP — up to ${MAX_AVATAR_MB}MB</p>
          </div>

          <div class="dc-onboarding__handle-block">
            <p class="dc-onboarding__eyebrow">Your community profile will live at</p>
            <p class="dc-onboarding__handle">
              <span class="dc-onboarding__handle-url">${this.#renderProfileUrl(state.handle)}</span>
              ${this.#profileBaseUrl()
                ? html`<button
                    class="dc-onboarding__handle-copy"
                    type="button"
                    @click=${() => this.#handleCopyProfileUrl(state.handle)}
                    title="${this._showCopiedFeedback ? 'Copied!' : 'Copy link'}"
                    aria-label="${this._showCopiedFeedback ? 'Link copied to clipboard' : 'Copy profile link to clipboard'}"
                  >
                    ${this._showCopiedFeedback ? iconCheck : iconCopy}
                  </button>`
                : ""}
            </p>
            <p class="dc-onboarding__hint">
              Need to change this?
              <a href=${this.#devrelContactHref()} target=${ifDefined(target)} rel=${ifDefined(rel)}>${this.#devrelContactLabel()}</a>
              and we'll help you out.
            </p>
          </div>
        </div>

        <form @submit=${this.#handleDetailsSubmit}>
          <div class="dc-form-field">
            <label for="dc-onboarding-bio">A brief bio for your profile page</label>
            <textarea
              id="dc-onboarding-bio"
              rows="4"
              maxlength="${MAX_BIO_LENGTH}"
              .value=${this._bioDraft}
              @input=${(event: Event) => (this._bioDraft = (event.target as HTMLTextAreaElement).value)}
            ></textarea>
            <p class="dc-onboarding__bio-count ${this._bioDraft.length >= MAX_BIO_LENGTH ? 'dc-onboarding__bio-count--limit' : ''}">
              ${this._bioDraft.length}/${MAX_BIO_LENGTH}
            </p>
          </div>
          <div class="dc-onboarding__actions">
            <button class="btn is-blue" type="submit">Continue</button>
          </div>
        </form>
      </section>
    `;
  }

  #renderStepFeeds(): TemplateResult {
    return html`
      <section class="dc-onboarding__step">
        ${this.#renderStepIndicator()}
        <h2>Your feeds</h2>
        <p>Add your blog, LinkedIn, or other social links — you can always change these later from your account page.</p>
        <dc-feed-manager restrict-hide-to-sphere></dc-feed-manager>
        <div class="dc-onboarding__actions">
          <button class="btn" type="button" @click=${this.#back}>Back</button>
          <button class="btn is-blue" type="button" @click=${this.#finish}>Finish</button>
        </div>
      </section>
    `;
  }

  #renderStep(): TemplateResult {
    const state = this._onboardingState;
    if (!state) return html``;

    switch (this._step) {
      case 1:
        return this.#renderStepDetails(state);
      case 2:
      default:
        return this.#renderStepFeeds();
    }
  }

  render() {
    switch (this._phase) {
      case "loading":
        return html`<p class="dc-onboarding__status" role="status">Loading…</p>`;
      case "error":
        return html`
          <div class="dc-onboarding__status dc-onboarding__status--error" role="alert">
            <p>${this._errorMessage}</p>
            <button class="btn is-blue" type="button" @click=${() => this.#retryAction?.()}>Try again</button>
          </div>
        `;
      case "done":
        return html`<p class="dc-onboarding__status">All set! Taking you to your profile…</p>`;
      case "saving":
      case "ready":
      default:
        return this.#renderStep();
    }
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: OnboardingWizardElement;
  }
}
