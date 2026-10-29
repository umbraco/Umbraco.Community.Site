import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { ProfileOnboardingService } from "../../services/profile-onboarding.service.js";

const elementName = "dc-profile-visibility-toggle";

let instanceCount = 0;

/**
 * A "Show my profile" switch on the Account page. Profiles start hidden, so only the owner
 * can see them until this is switched on. The switch moves as soon as it is clicked and
 * moves back if saving fails.
 */
@customElement(elementName)
export class ProfileVisibilityToggleElement extends LitElement {
  /** The stored state when the page rendered. */
  @property({ type: Boolean, attribute: "profile-visible" })
  profileVisible = false;

  @state()
  private _visible = false;

  @state()
  private _saving = false;

  @state()
  private _statusMessage = "";

  @state()
  private _errorMessage = "";

  readonly #inputId = `${elementName}-input-${++instanceCount}`;
  readonly #hintId = `${elementName}-hint-${instanceCount}`;

  // Render in light DOM so the site's existing styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    this._visible = this.profileVisible;
  }

  async #onChange(event: Event) {
    const input = event.target as HTMLInputElement;

    // Ignore further clicks while a save is in flight. The input is not disabled, so a
    // keyboard user keeps focus on it.
    if (this._saving) {
      input.checked = this._visible;
      return;
    }

    const previous = this._visible;
    const next = input.checked;

    this._visible = next;
    this._saving = true;
    this._errorMessage = "";
    this._statusMessage = "";

    try {
      await ProfileOnboardingService.setProfileVisible(next);
      this._statusMessage = next ? "Your profile is now visible." : "Your profile is now hidden.";
    } catch (error) {
      this._visible = previous;
      input.checked = previous;
      this._errorMessage =
        error instanceof Error && error.message
          ? error.message
          : "Couldn't save this change. Please try again.";
    } finally {
      this._saving = false;
    }
  }

  render(): TemplateResult {
    return html`
      <div class="dc-profile-visibility-toggle">
        <label class="dc-profile-visibility-toggle__label" for=${this.#inputId}>
          <input
            id=${this.#inputId}
            class="dc-profile-visibility-toggle__input"
            type="checkbox"
            role="switch"
            aria-describedby=${this.#hintId}
            .checked=${this._visible}
            aria-busy=${this._saving ? "true" : "false"}
            @change=${this.#onChange}
          />
          <span class="dc-profile-visibility-toggle__track" aria-hidden="true"></span>
          <span class="dc-profile-visibility-toggle__text">Show my profile</span>
        </label>
        <p id=${this.#hintId} class="dc-profile-visibility-toggle__hint">
          ${this._visible
            ? "Your profile is public. Anyone in the world can see it."
            : "Your profile is hidden. Only you can see it, so you can finish editing it first. Turning this on makes it public, so anyone in the world can see it, not just you."}
        </p>
        <p class="dc-profile-visibility-toggle__status" role="status" aria-live="polite">${this._statusMessage}</p>
        ${this._errorMessage
          ? html`<p class="dc-profile-visibility-toggle__error" role="alert">${this._errorMessage}</p>`
          : ""}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileVisibilityToggleElement;
  }
}
