import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service.js";
import { iconEye, iconEyeOff, iconX } from "../../svg/lucide-icons.js";

const elementName = "dc-feed-manager";

@customElement(elementName)
export class FeedManagerElement extends LitElement {
  /**
   * When set, only platform-supplied feeds can be hidden — member-added feeds can only be
   * removed. Used during onboarding, where hiding something you just added yourself (before
   * you've even seen your profile) is a confusing first impression; the Account page doesn't
   * set this, so once a member has seen their profile they can hide any feed.
   */
  @property({ attribute: "restrict-hide-to-platform", type: Boolean })
  restrictHideToPlatform = false;

  @state()
  private _feeds: MemberFeed[] = [];

  @state()
  private _loading = true;

  @state()
  private _errorMessage = "";

  @state()
  private _platformDraft = "";

  @state()
  private _urlDraft = "";

  @state()
  private _removingId: number | null = null;

  @state()
  private _removeReasonDraft = "";

  // Render in light DOM so the site's existing form/button styles apply without duplication.
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
      this._feeds = await MemberFeedsService.list();
      this._errorMessage = "";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't load your feeds.";
    } finally {
      this._loading = false;
    }
  }

  async #handleAdd(event: Event) {
    event.preventDefault();
    if (!this._platformDraft.trim() || !this._urlDraft.trim()) return;

    try {
      const feed = await MemberFeedsService.add(this._platformDraft.trim(), this._urlDraft.trim());
      this._feeds = [...this._feeds, feed];
      this._platformDraft = "";
      this._urlDraft = "";
      this._errorMessage = "";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't add that feed.";
    }
  }

  async #toggleHidden(feed: MemberFeed) {
    try {
      await MemberFeedsService.setHidden(feed.id, !feed.isHidden);
      this._feeds = this._feeds.map((f) => (f.id === feed.id ? { ...f, isHidden: !f.isHidden } : f));
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't update that feed.";
    }
  }

  #startRemove(feed: MemberFeed) {
    this._removingId = feed.id;
    this._removeReasonDraft = "";
  }

  #cancelRemove() {
    this._removingId = null;
    this._removeReasonDraft = "";
  }

  async #confirmRemove(id: number) {
    try {
      await MemberFeedsService.remove(id, this._removeReasonDraft.trim() || undefined);
      this._feeds = this._feeds.filter((f) => f.id !== id);
      this._removingId = null;
      this._removeReasonDraft = "";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't remove that feed.";
    }
  }

  #canHide(feed: MemberFeed): boolean {
    return !this.restrictHideToPlatform || feed.source === "Platform";
  }

  #renderFeed(feed: MemberFeed): TemplateResult {
    if (this._removingId === feed.id) {
      return html`
        <li class="dc-feed-manager__item dc-feed-manager__item--removing">
          <p id="dc-feed-manager-reason-label-${feed.id}">This isn't your <strong>${feed.platform}</strong>? We'll flag it so it can be corrected. Add any detail (optional):</p>
          <textarea
            rows="2"
            aria-labelledby="dc-feed-manager-reason-label-${feed.id}"
            .value=${this._removeReasonDraft}
            @input=${(event: Event) => (this._removeReasonDraft = (event.target as HTMLTextAreaElement).value)}
          ></textarea>
          <div class="dc-feed-manager__item-actions">
            <button class="btn" type="button" @click=${this.#cancelRemove}>Cancel</button>
            <button class="btn is-blue" type="button" @click=${() => this.#confirmRemove(feed.id)}>
              Confirm — this isn't me
            </button>
          </div>
        </li>
      `;
    }

    return html`
      <li class="dc-feed-manager__item ${feed.isHidden ? "dc-feed-manager__item--hidden" : ""}">
        <div class="dc-feed-manager__item-info">
          <span class="dc-feed-manager__platform">${feed.platform}</span>
          <a class="dc-feed-manager__url" href=${feed.url} target="_blank" rel="noopener noreferrer">${feed.url}</a>
          ${feed.isHidden ? html`<span class="dc-feed-manager__badge">Hidden from profile</span>` : ""}
        </div>
        <div class="dc-feed-manager__item-actions">
          ${this.#canHide(feed)
            ? html`<button class="link-action" type="button" @click=${() => this.#toggleHidden(feed)}>
                ${feed.isHidden ? iconEye : iconEyeOff}
                ${feed.isHidden ? "Show on profile" : "Hide from profile"}
              </button>`
            : ""}
          <button class="link-action" type="button" @click=${() => this.#startRemove(feed)}>
            ${iconX} This isn't me
          </button>
        </div>
      </li>
    `;
  }

  #renderGroup(title: string, feeds: MemberFeed[]): TemplateResult | string {
    if (feeds.length === 0) return "";

    return html`
      <div class="dc-feed-manager__group">
        <h3 class="dc-feed-manager__group-title">${title}</h3>
        <ul class="dc-feed-manager__list">
          ${feeds.map((feed) => this.#renderFeed(feed))}
        </ul>
      </div>
    `;
  }

  #renderAddForm(): TemplateResult {
    return html`
      <form class="dc-feed-manager__add-form" @submit=${this.#handleAdd}>
        <div class="dc-form-field">
          <label for="dc-feed-manager-platform">Platform</label>
          <input
            id="dc-feed-manager-platform"
            type="text"
            placeholder="LinkedIn, Blog, Mastodon…"
            required
            .value=${this._platformDraft}
            @input=${(event: Event) => (this._platformDraft = (event.target as HTMLInputElement).value)}
          />
        </div>
        <div class="dc-form-field">
          <label for="dc-feed-manager-url">URL</label>
          <input
            id="dc-feed-manager-url"
            type="url"
            placeholder="https://…"
            required
            .value=${this._urlDraft}
            @input=${(event: Event) => (this._urlDraft = (event.target as HTMLInputElement).value)}
          />
        </div>
        <button class="btn is-blue" type="submit">Add feed</button>
      </form>
    `;
  }

  render() {
    if (this._loading) {
      return html`<p role="status">Loading your feeds…</p>`;
    }

    const platformFeeds = this._feeds.filter((feed) => feed.source === "Platform");
    const memberFeeds = this._feeds.filter((feed) => feed.source === "Member");

    return html`
      ${this._errorMessage ? html`<p class="dc-feed-manager__error" role="alert">${this._errorMessage}</p>` : ""}
      ${this._feeds.length === 0
        ? html`<p class="dc-feed-manager__empty">
            We don't have any feeds linked to your profile yet — add your blog, LinkedIn, or other social links below.
          </p>`
        : html`
            <p class="dc-feed-manager__hint">
              Hiding a feed keeps it here for you to manage, but removes it from your public profile.
            </p>
            ${this.#renderGroup("Synced automatically", platformFeeds)}
            ${this.#renderGroup("Added by you", memberFeeds)}
          `}
      ${this.#renderAddForm()}
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: FeedManagerElement;
  }
}
