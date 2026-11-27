import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import { MemberFeedsService } from "../../services/member-feeds.service.js";
import { ProfileLinksService, type LinkSuggestion } from "../../services/profile-links.service.js";
import { onFaviconError, onFaviconLoad } from "../../util/favicon.js";

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

  /** The suggestion whose "This is not me" confirmation is open. Only one is open at a time. */
  @state()
  private _confirmingUrl: string | null = null;

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

  /** Loads the suggestions. A quiet reload keeps the list on screen (and the focus in it) while it fetches. */
  async #load(quiet = false) {
    this._loading = !quiet;
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
      // MemberFeedsService.add announces the new link, so the member's list of links and the profile header show it.
      // Only once it is saved does the suggestion leave this list; if saving fails it stays, with the error.
      await MemberFeedsService.add(platformOf(suggestion), suggestion.url);
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't add this link. Please try again.";
      this._busyUrl = null;
      return;
    }

    this._suggestions = this._suggestions.filter((s) => s.url !== suggestion.url);
    this._statusMessage = `${labelOf(suggestion)} is added to your links and shows on your profile.`;
    this._busyUrl = null;
    await this.#load(true);
  }

  /** The list item for a suggestion, found by its address (compared as text, so the address needs no escaping). */
  #itemFor(url: string): HTMLElement | undefined {
    return Array.from(this.querySelectorAll<HTMLElement>("[data-suggestion-url]")).find((item) => item.dataset.suggestionUrl === url);
  }

  /** Opens the "This is not me" confirmation in place of the suggestion, with Cancel focused so Enter can't confirm by accident. */
  async #startNotMe(suggestion: LinkSuggestion) {
    if (this._busyUrl) return;
    this._confirmingUrl = suggestion.url;
    this._statusMessage = "";
    this._errorMessage = "";
    await this.updateComplete;
    this.#itemFor(suggestion.url)?.querySelector<HTMLButtonElement>('[data-action="cancel-not-me"]')?.focus();
  }

  /** Closes the confirmation without saving anything and puts focus back on the suggestion's "This is not me" button. */
  async #cancelNotMe(suggestion: LinkSuggestion) {
    if (this._busyUrl) return;
    this._confirmingUrl = null;
    await this.updateComplete;
    this.#itemFor(suggestion.url)?.querySelector<HTMLButtonElement>('[data-action="not-me"]')?.focus();
  }

  /**
   * Marks the suggestion as not the member's: activity found through it stops showing on their profile and it isn't
   * suggested again. If saving fails the confirmation stays open (with the error), so they can try again or cancel.
   */
  async #confirmNotMe(suggestion: LinkSuggestion) {
    if (this._busyUrl) return;
    const index = this._suggestions.findIndex((s) => s.url === suggestion.url);
    this._busyUrl = suggestion.url;
    this._statusMessage = "";
    this._errorMessage = "";
    try {
      await ProfileLinksService.notMe(suggestion.url);
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't save that. Please try again.";
      this._busyUrl = null;
      return;
    }

    this._suggestions = this._suggestions.filter((s) => s.url !== suggestion.url);
    this._confirmingUrl = null;
    this._busyUrl = null;
    this._statusMessage = `Thanks. ${labelOf(suggestion)} is marked as not yours and won't show on your profile.`;

    // The item is gone, so put focus on the suggestion that took its place (or the list's closing message) rather than losing it.
    await this.updateComplete;
    const next = this._suggestions[Math.min(index, this._suggestions.length - 1)];
    const target =
      (next && this.#itemFor(next.url)?.querySelector<HTMLElement>('[data-action="not-me"]')) ??
      this.querySelector<HTMLElement>(".dc-link-suggestions__hint");
    target?.focus();
  }

  #renderSuggestion(suggestion: LinkSuggestion): TemplateResult {
    if (this._confirmingUrl === suggestion.url) {
      return this.#renderNotMeConfirmation(suggestion);
    }

    const label = labelOf(suggestion);
    const busy = this._busyUrl !== null;
    return html`
      <li class="dc-link-suggestions__item" data-suggestion-url=${suggestion.url}>
        <div class="dc-link-suggestions__info">
          <span class="dc-link-suggestions__label">
            ${suggestion.faviconUrl
              ? html`<img
                  class="dc-feed-manager__favicon"
                  src=${suggestion.faviconUrl}
                  alt=""
                  width="16"
                  height="16"
                  loading="lazy"
                  @load=${onFaviconLoad}
                  @error=${onFaviconError}
                />`
              : nothing}
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
          <button
            class="link-action"
            type="button"
            data-action="not-me"
            ?disabled=${busy}
            aria-label="${label} is not me"
            @click=${() => this.#startNotMe(suggestion)}
          >
            This is not me
          </button>
        </div>
      </li>
    `;
  }

  /**
   * Asks before marking a suggestion as not the member's, in place of the suggestion, saying plainly what happens. Like
   * "This isn't me" on the member's own links: Cancel comes first and gets focus, and the confirm button says what it does.
   * While saving, the buttons are only marked aria-disabled (not disabled), so focus stays on the confirm button.
   */
  #renderNotMeConfirmation(suggestion: LinkSuggestion): TemplateResult {
    const label = labelOf(suggestion);
    const id = this._suggestions.indexOf(suggestion);
    const titleId = `dc-link-suggestions-not-me-title-${id}`;
    const descriptionId = `dc-link-suggestions-not-me-description-${id}`;
    const busy = this._busyUrl === suggestion.url;

    return html`
      <li class="dc-link-suggestions__item dc-link-suggestions__item--confirming" data-suggestion-url=${suggestion.url}>
        <div class="dc-feed-manager__remove" role="group" aria-labelledby=${titleId} aria-describedby=${descriptionId}>
          <p id=${titleId} class="dc-feed-manager__remove-title">Is <strong>${label}</strong> not yours?</p>
          <div id=${descriptionId} class="dc-feed-manager__remove-description">
            <p>
              We'll remove it from your suggestions and won't show it on your profile: anything we found through
              <span class="dc-feed-manager__url">${suggestion.url}</span> will stop appearing. We won't suggest it again.
            </p>
          </div>
          <div class="dc-link-suggestions__actions">
            <button class="btn" type="button" data-action="cancel-not-me" aria-disabled=${busy ? "true" : "false"} @click=${() => this.#cancelNotMe(suggestion)}>
              Cancel
            </button>
            <button
              class="btn dc-feed-manager__confirm-remove"
              type="button"
              data-action="confirm-not-me"
              aria-disabled=${busy ? "true" : "false"}
              @click=${() => this.#confirmNotMe(suggestion)}
            >
              Yes, this isn't me
            </button>
          </div>
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
              : html`<p class="dc-link-suggestions__hint" tabindex="-1">We haven't found any other links for you right now.</p>`}
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
