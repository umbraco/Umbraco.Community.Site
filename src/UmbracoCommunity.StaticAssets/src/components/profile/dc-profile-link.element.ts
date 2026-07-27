import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { iconCopy, iconCheck } from "../../svg/lucide-icons.js";
import { copyToClipboard } from "../../util/clipboard.js";

const elementName = "dc-profile-link";

/**
 * A copyable "here's your public profile link" chip — the boilerplate part of the URL
 * (scheme, host, base path) recedes, the last path segment (the member's handle) carries the
 * emphasis, with a copy-to-clipboard button alongside. Shared between the onboarding wizard's
 * "Your details" step and the Account page's "your public profile" section, so this pattern
 * (and its clipboard/feedback logic) only lives in one place.
 */
@customElement(elementName)
export class ProfileLinkElement extends LitElement {
  /** The full, absolute profile URL (scheme + host + path), e.g. "https://.../profiles/octocat". */
  @property({ type: String })
  url = "";

  @state()
  private _showCopiedFeedback = false;

  #copiedTimeout?: ReturnType<typeof setTimeout>;

  // Render in light DOM so the site's existing button styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this.#copiedTimeout) {
      clearTimeout(this.#copiedTimeout);
    }
  }

  // Split at the last "/" so the handle — the part that's actually the member's own — can be
  // styled distinctly from the boilerplate scheme/host/base-path prefix.
  #base(): string {
    const index = this.url.lastIndexOf("/");
    return index === -1 ? "" : this.url.slice(0, index + 1);
  }

  #slug(): string {
    const index = this.url.lastIndexOf("/");
    return index === -1 ? this.url : this.url.slice(index + 1);
  }

  async #handleCopy() {
    const copied = await copyToClipboard(this.url);
    if (!copied) return;

    this._showCopiedFeedback = true;
    if (this.#copiedTimeout) {
      clearTimeout(this.#copiedTimeout);
    }
    this.#copiedTimeout = setTimeout(() => {
      this._showCopiedFeedback = false;
    }, 2000);
  }

  render(): TemplateResult {
    if (!this.url) return html``;

    return html`
      <p class="profile-link">
        <span class="profile-link__url">
          <span class="profile-link__base">${this.#base()}</span
          ><span class="profile-link__slug">${this.#slug()}</span>
        </span>
        <button
          class="link-action profile-link__copy"
          type="button"
          @click=${this.#handleCopy}
          title="${this._showCopiedFeedback ? "Copied!" : "Copy link"}"
          aria-label="${this._showCopiedFeedback ? "Link copied to clipboard" : "Copy profile link to clipboard"}"
        >
          ${this._showCopiedFeedback ? iconCheck : iconCopy}
        </button>
      </p>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileLinkElement;
  }
}
