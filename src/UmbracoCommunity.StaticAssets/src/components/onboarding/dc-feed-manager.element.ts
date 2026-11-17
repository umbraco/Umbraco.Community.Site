import { LitElement, html, type TemplateResult } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { classMap } from "lit/directives/class-map.js";
import {
  isPlatformSourced,
  MemberFeedsService,
  type MemberFeed,
  type MemberFeedMatchStatus,
} from "../../services/member-feeds.service.js";
import { iconEye, iconEyeOff, iconX } from "../../svg/lucide-icons.js";
import { moveItem, renderMoveButtons, SortableDragController } from "../sortable/sortable-list.js";

const elementName = "dc-feed-manager";

const matchStatusLabels: Record<MemberFeedMatchStatus, string> = {
  Confirmed: "Matches your profile",
  Held: "Waiting for a check",
  Rejected: "Marked as not yours",
};

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
  private _editingId: number | null = null;

  @state()
  private _editPlatformDraft = "";

  @state()
  private _editUrlDraft = "";

  /** Announces reordering to screen reader users. */
  @state()
  private _statusMessage = "";

  @state()
  private _removingId: number | null = null;

  @state()
  private _removeReasonDraft = "";

  /** Links are dragged within their own group (synced or added by the member), as the order is kept per group. */
  readonly #drag = new SortableDragController<number>(
    this,
    (id, targetId, after) => void this.#moveBefore(id, targetId, after),
    (id, targetId) => this.#groupOf(id) === this.#groupOf(targetId)
  );

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

  #startEdit(feed: MemberFeed) {
    this._editingId = feed.id;
    this._editPlatformDraft = feed.platform;
    this._editUrlDraft = feed.url;
  }

  #cancelEdit() {
    this._editingId = null;
  }

  async #saveEdit(event: Event, id: number) {
    event.preventDefault();
    try {
      const updated = await MemberFeedsService.update(id, this._editPlatformDraft.trim(), this._editUrlDraft.trim());
      this._feeds = this._feeds.map((f) => (f.id === id ? updated : f));
      this._editingId = null;
      this._errorMessage = "";
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't save that link.";
    }
  }

  #groupOf(id: number): boolean | undefined {
    const feed = this._feeds.find((f) => f.id === id);
    return feed ? isPlatformSourced(feed) : undefined;
  }

  /** Moves a link to just before (or after) another one, then stores the new order. */
  async #moveBefore(feedId: number, targetId: number, after = false, focus?: "up" | "down") {
    const previous = this._feeds;
    const reordered = moveItem(previous, (f) => f.id, feedId, targetId, after);
    if (!reordered) return;

    this._feeds = reordered;
    const moved = reordered.find((f) => f.id === feedId)!;
    const group = reordered.filter((f) => isPlatformSourced(f) === isPlatformSourced(moved));
    this._statusMessage = `${moved.platform} moved to position ${group.indexOf(moved) + 1} of ${group.length}.`;

    if (focus) {
      // Keep keyboard focus on the link that moved.
      await this.updateComplete;
      this.#focusMoveButton(feedId, focus);
    }

    try {
      await MemberFeedsService.reorder(reordered.map((f) => f.id));
      this._errorMessage = "";
    } catch (error) {
      this._feeds = previous;
      this._statusMessage = "";
      this._errorMessage = error instanceof Error ? error.message : "Couldn't save the new order.";
    }
  }

  #moveByStep(feed: MemberFeed, group: MemberFeed[], step: -1 | 1) {
    const neighbour = group[group.indexOf(feed) + step];
    if (neighbour) void this.#moveBefore(feed.id, neighbour.id, step === 1, step === -1 ? "up" : "down");
  }

  #focusMoveButton(feedId: number, preferred: "up" | "down") {
    const row = this.querySelector<HTMLElement>(`[data-feed-id="${feedId}"]`);
    const button =
      row?.querySelector<HTMLButtonElement>(`[data-action="${preferred}"]:not(:disabled)`) ??
      row?.querySelector<HTMLButtonElement>(`[data-action="up"]:not(:disabled), [data-action="down"]:not(:disabled)`);
    button?.focus();
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
    return !this.restrictHideToPlatform || isPlatformSourced(feed);
  }

  #renderFeed(feed: MemberFeed, group: MemberFeed[]): TemplateResult {
    // Platform-synced feeds were matched to this member automatically, so removing one can mean
    // "that's a mismatch, not mine" — the copy reflects that so the flag is meaningful when it's
    // reported back. Member-added feeds were added deliberately, so removing one is just tidying
    // up, not disputing an identity match.
    const isThisFeedPlatformSourced = isPlatformSourced(feed);

    if (this._removingId === feed.id) {
      return html`
        <li class="dc-feed-manager__item dc-feed-manager__item--removing">
          <p id="dc-feed-manager-reason-label-${feed.id}">
            ${isThisFeedPlatformSourced
              ? html`This isn't your <strong>${feed.platform}</strong>? We'll flag it so it can be corrected. Add any detail (optional):`
              : html`Remove <strong>${feed.platform}</strong>? Let us know why (optional):`}
          </p>
          <textarea
            rows="2"
            aria-labelledby="dc-feed-manager-reason-label-${feed.id}"
            .value=${this._removeReasonDraft}
            @input=${(event: Event) => (this._removeReasonDraft = (event.target as HTMLTextAreaElement).value)}
          ></textarea>
          <div class="dc-feed-manager__item-actions">
            <button class="btn" type="button" @click=${this.#cancelRemove}>Cancel</button>
            <button class="btn is-blue" type="button" @click=${() => this.#confirmRemove(feed.id)}>
              ${isThisFeedPlatformSourced ? "Confirm — this isn't me" : "Confirm removal"}
            </button>
          </div>
        </li>
      `;
    }

    if (this._editingId === feed.id) {
      return html`
        <li class="dc-feed-manager__item dc-feed-manager__item--editing">
          <form class="dc-feed-manager__add-form" @submit=${(event: Event) => this.#saveEdit(event, feed.id)}>
            <div class="dc-form-field">
              <label for="dc-feed-manager-edit-platform-${feed.id}">Platform</label>
              <input
                id="dc-feed-manager-edit-platform-${feed.id}"
                type="text"
                required
                .value=${this._editPlatformDraft}
                @input=${(event: Event) => (this._editPlatformDraft = (event.target as HTMLInputElement).value)}
              />
            </div>
            <div class="dc-form-field">
              <label for="dc-feed-manager-edit-url-${feed.id}">URL</label>
              <input
                id="dc-feed-manager-edit-url-${feed.id}"
                type="url"
                required
                .value=${this._editUrlDraft}
                @input=${(event: Event) => (this._editUrlDraft = (event.target as HTMLInputElement).value)}
              />
            </div>
            <div class="dc-feed-manager__item-actions">
              <button class="btn" type="button" @click=${this.#cancelEdit}>Cancel</button>
              <button class="btn is-blue" type="submit">Save</button>
            </div>
          </form>
        </li>
      `;
    }

    const position = group.indexOf(feed);
    const statusLabel =
      feed.status === "Hidden" ? "Hidden from profile" : feed.status === "Unreachable" ? "Couldn't be reached" : "Shown on profile";

    return html`
      <li
        class=${classMap({
          ...this.#drag.rowClasses(feed.id),
          "dc-feed-manager__item": true,
          "dc-feed-manager__item--sortable": true,
          "dc-feed-manager__item--hidden": feed.isHidden,
        })}
        data-sortable-row
        data-feed-id=${feed.id}
        @dragover=${(event: DragEvent) => this.#drag.over(event, feed.id)}
        @drop=${(event: DragEvent) => this.#drag.drop(event, feed.id)}
      >
        ${group.length > 1 ? this.#drag.handle(feed.id) : ""}
        <div class="dc-feed-manager__item-main">
          <div class="dc-feed-manager__item-info">
            <span class="dc-feed-manager__platform">
              ${feed.faviconUrl ? html`<img class="dc-feed-manager__favicon" src=${feed.faviconUrl} alt="" width="16" height="16" loading="lazy" />` : ""}
              ${feed.platform}
            </span>
            <a class="dc-feed-manager__url no-link-animation" href=${feed.url} target="_blank" rel="noopener noreferrer">${feed.url}</a>
            <span class="dc-feed-manager__badge dc-feed-manager__badge--${feed.status.toLowerCase()}">${statusLabel}</span>
            ${feed.matchStatus && matchStatusLabels[feed.matchStatus]
              ? html`<span class="dc-feed-manager__match dc-feed-manager__match--${feed.matchStatus.toLowerCase()}">${matchStatusLabels[feed.matchStatus]}</span>`
              : ""}
          </div>
          <div class="dc-feed-manager__item-actions">
            ${group.length > 1
              ? renderMoveButtons({
                  name: feed.platform,
                  isFirst: position === 0,
                  isLast: position === group.length - 1,
                  onMove: (step) => this.#moveByStep(feed, group, step),
                })
              : ""}
            <button class="link-action" type="button" @click=${() => this.#startEdit(feed)}>Edit</button>
            ${this.#canHide(feed)
              ? html`<button class="link-action" type="button" @click=${() => this.#toggleHidden(feed)}>
                  ${feed.isHidden ? iconEye : iconEyeOff}
                  ${feed.isHidden ? "Show on profile" : "Hide from profile"}
                </button>`
              : ""}
            <button class="link-action" type="button" @click=${() => this.#startRemove(feed)}>
              ${iconX} ${isThisFeedPlatformSourced ? "This isn't me" : "Remove"}
            </button>
          </div>
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
          ${feeds.map((feed) => this.#renderFeed(feed, feeds))}
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
            placeholder="https://…" pattern="https://.*"
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

    const platformFeeds = this._feeds.filter((feed) => isPlatformSourced(feed));
    const memberFeeds = this._feeds.filter((feed) => !isPlatformSourced(feed));

    return html`
      ${this._errorMessage ? html`<p class="dc-feed-manager__error" role="alert">${this._errorMessage}</p>` : ""}
      <p class="dc-feed-manager__status sr-only" role="status" aria-live="polite">${this._statusMessage}</p>
      ${this._feeds.length === 0
        ? html`<p class="dc-feed-manager__empty">
            We don't have any feeds linked to your profile yet — add your blog, LinkedIn, or other social links below.
          </p>`
        : html`
            <p class="dc-feed-manager__hint">
              Hiding a feed keeps it here for you to manage, but removes it from your public profile. Drag a link by its handle,
              or use the up and down buttons, to change the order it appears in.
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
