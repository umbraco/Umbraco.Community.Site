import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { ifDefined } from "lit/directives/if-defined.js";
import {
  ProfileOnboardingService,
  type OnboardingState,
} from "../../services/profile-onboarding.service.js";

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

  // Whatever action most recently failed — "Try again" re-runs exactly that, rather than
  // always restarting from the beginning (which would just reload the same draft state and
  // strand the member on whichever step they were on when the retry loop began).
  #retryAction: (() => void) | null = null;

  // Render in light DOM so the site's existing form/button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    void this.#start();
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
    await this.#uploadAvatar(file);
  }

  async #uploadAvatar(file: File) {
    this._phase = "saving";
    try {
      const result = await ProfileOnboardingService.uploadAvatar(file);
      this._onboardingState = this._onboardingState
        ? { ...this._onboardingState, avatarUrl: result.avatarUrl }
        : this._onboardingState;
      this._phase = "ready";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't upload that image. Please try again.";
      this.#retryAction = () => this.#uploadAvatar(file);
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

  #renderStepDetails(state: OnboardingState): TemplateResult {
    const target = this.devrelContactTarget || undefined;
    const rel = target === "_blank" ? "noopener noreferrer" : undefined;
    return html`
      <section class="dc-onboarding__step">
        ${this.#renderStepIndicator()}
        <h2>Your details</h2>

        <div class="dc-onboarding__handle-block">
          <p>Your community profile will live at:</p>
          <p class="dc-onboarding__handle">@${state.handle}</p>
          <p class="dc-onboarding__hint">
            Need to change this?
            <a href=${this.#devrelContactHref()} target=${ifDefined(target)} rel=${ifDefined(rel)}>Email the DevRel team</a>
            and we'll help you out.
          </p>
        </div>

        <div class="dc-onboarding__avatar-block">
          <img class="dc-onboarding__avatar-preview" src=${state.avatarUrl} alt="" width="120" height="120" />
          <label class="btn" for="dc-onboarding-avatar-input">Upload a different image</label>
          <input
            id="dc-onboarding-avatar-input"
            type="file"
            accept="image/gif,image/jpeg,image/png,image/tiff,image/webp"
            class="sr-only"
            @change=${this.#handleAvatarChange}
          />
        </div>

        <form @submit=${this.#handleDetailsSubmit}>
          <div class="dc-form-field">
            <label for="dc-onboarding-bio">A brief bio for your profile page</label>
            <textarea
              id="dc-onboarding-bio"
              rows="4"
              maxlength="1000"
              .value=${this._bioDraft}
              @input=${(event: Event) => (this._bioDraft = (event.target as HTMLTextAreaElement).value)}
            ></textarea>
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
