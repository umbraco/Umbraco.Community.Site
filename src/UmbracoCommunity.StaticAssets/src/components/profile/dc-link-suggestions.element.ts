import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import { MemberFeedsService } from "../../services/member-feeds.service.js";
import { ProfileLinksService, type LinkSuggestion } from "../../services/profile-links.service.js";

const elementName = "dc-link-suggestions";

/** What the suggestion is called on screen: its name, else its platform, else its address. */
function labelOf(suggestion: LinkSuggestion): string {
  return suggestion.name || suggestion.platform || suggestion.url;
}

/** The platform name stored with the link when the member adds it. */
function platformOf(suggestion: LinkSuggestion): string {
  return suggestion.platform || suggestion.name || (suggestion.kind === "Feed" ? "Feed" : "Link");
}

/**
 * Lists accounts and feeds that look like the member's. Each can be added to their links, or marked as not theirs,
 * which stops activity from it showing on their profile.
 */
@customElement(elementName)
export class LinkSuggestionsElement extends LitElement {
  @state()
  private _suggestions: LinkSuggestion[] = [];

  @state()
  private _loading = true;

  @state()
  private _busyUrl: string | null = null;

  @state()
  private _statusMessage = "";

  @state()
  private _errorMessage = "";

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
      this._suggestions = await ProfileLinksService.suggestions();
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't load the links we found.";
    } finally {
      this._loading = false;
    }
  }

  async #add(suggestion: LinkSuggestion) {
    this._busyUrl = suggestion.url;
    this._statusMessage = "";
    this._errorMessage = "";
    try {
      await MemberFeedsService.add(platformOf(suggestion), suggestion.url);
      this._suggestions = this._suggestions.filter((s) => s.url !== suggestion.url);
      this._statusMessage = `${labelOf(suggestion)} is added to your links.`;
      await this.#load();
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't add this link. Please try again.";
    } finally {
      this._busyUrl = null;
    }
  }

  async #notMe(suggestion: LinkSuggestion) {
    this._busyUrl = suggestion.url;
    this._statusMessage = "";
    this._errorMessage = "";
    try {
      await ProfileLinksService.notMe(suggestion.url);
      this._suggestions = this._suggestions.filter((s) => s.url !== suggestion.url);
      this._statusMessage = `Thanks. ${labelOf(suggestion)} is marked as not yours and won't show on your profile.`;
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't save that. Please try again.";
    } finally {
      this._busyUrl = null;
    }
  }

  #renderSuggestion(suggestion: LinkSuggestion): TemplateResult {
    const label = labelOf(suggestion);
    const busy = this._busyUrl === suggestion.url;
    return html`
      <li class="dc-link-suggestions__item">
        <div class="dc-link-suggestions__info">
          <span class="dc-link-suggestions__label">
            ${label}${suggestion.platform && suggestion.platform !== label
              ? html` <span class="dc-link-suggestions__platform">(${suggestion.platform})</span>`
              : nothing}
          </span>
          <a class="dc-link-suggestions__url no-link-animation" href=${suggestion.url} target="_blank" rel="noopener noreferrer">${suggestion.url}</a>
        </div>
        <div class="dc-link-suggestions__actions">
          <button class="btn is-blue" type="button" ?disabled=${busy} aria-label="Add ${label} to my links" @click=${() => this.#add(suggestion)}>
            Add to my links
          </button>
          <button class="link-action" type="button" ?disabled=${busy} aria-label="${label} is not me" @click=${() => this.#notMe(suggestion)}>
            This is not me
          </button>
        </div>
      </li>
    `;
  }

  render(): TemplateResult {
    return html`
      <div class="dc-link-suggestions">
        ${this._loading
          ? html`<p class="dc-link-suggestions__hint">Loading…</p>`
          : this._suggestions.length > 0
            ? html`<ul class="dc-link-suggestions__list" aria-label="Links we found for you">
                ${this._suggestions.map((suggestion) => this.#renderSuggestion(suggestion))}
              </ul>`
            : this._errorMessage
              ? nothing
              : html`<p class="dc-link-suggestions__hint">We haven't found any other links for you right now.</p>`}
        <p class="dc-link-suggestions__status" role="status" aria-live="polite">${this._statusMessage}</p>
        ${this._errorMessage ? html`<p class="dc-link-suggestions__error" role="alert">${this._errorMessage}</p>` : nothing}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: LinkSuggestionsElement;
  }
}
