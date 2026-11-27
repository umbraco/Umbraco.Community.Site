import { LitElement, html, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import { classMap } from "lit/directives/class-map.js";
import { repeat } from "lit/directives/repeat.js";
import {
  isPlatformSourced,
  MEMBER_FEEDS_CHANGED_EVENT,
  MemberFeedsService,
  type MemberFeed,
  type MemberFeedMatchStatus,
  type MemberFeedsChangedDetail,
} from "../../services/member-feeds.service.js";
import { iconEye, iconEyeOff } from "../../svg/lucide-icons.js";
import { onFaviconError, onFaviconLoad } from "../../util/favicon.js";
import { moveItem, renderMoveButtons, SortableDragController } from "../sortable/sortable-list.js";

const elementName = "dc-feed-manager";

const matchStatusLabels: Record<MemberFeedMatchStatus, string> = {
  Confirmed: "Matches your profile",
  Held: "Waiting for a check",
  Rejected: "Marked as not yours",
};

@customElement(elementName)
export class FeedManagerElement extends LitElement {
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

  /** The link whose "This isn't me" confirmation is open. */
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

  /**
   * A link added anywhere on the page (such as "Add to my links" on a link we found) joins the list. Without this the
   * list only ever showed what it loaded on arrival, so an added suggestion vanished from both lists until a reload.
   */
  readonly #onFeedsChanged = (event: Event) => {
    const detail = (event as CustomEvent<MemberFeedsChangedDetail>).detail;
    if (detail?.change === "added" && detail.feed) {
      this.#upsert(detail.feed);
    }
  };

  connectedCallback() {
    super.connectedCallback();
    window.addEventListener(MEMBER_FEEDS_CHANGED_EVENT, this.#onFeedsChanged);
    void this.#load();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    window.removeEventListener(MEMBER_FEEDS_CHANGED_EVENT, this.#onFeedsChanged);
  }

  /** Adds a link to the end of the list, or replaces it when it is already there (so an add is never shown twice). */
  #upsert(feed: MemberFeed) {
    this._feeds = this._feeds.some((f) => f.id === feed.id)
      ? this._feeds.map((f) => (f.id === feed.id ? feed : f))
      : [...this._feeds, feed];
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
      this.#upsert(feed);
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

  /** Opens the "This isn't me" confirmation (from the row's edit mode), with Cancel focused so Enter can't remove by accident. */
  async #startRemove(feed: MemberFeed) {
    this._editingId = null;
    this._removingId = feed.id;
    this._removeReasonDraft = "";
    await this.updateComplete;
    this.querySelector<HTMLButtonElement>(`[data-feed-id="${feed.id}"] [data-action="cancel-remove"]`)?.focus();
  }

  async #cancelRemove(id: number) {
    this._removingId = null;
    this._removeReasonDraft = "";
    await this.updateComplete;
    this.querySelector<HTMLButtonElement>(`[data-feed-id="${id}"] [data-action="edit"]`)?.focus();
  }

  /**
   * Takes the link off the profile completely. The server removes it and marks its address as not the member's in one
   * request, so activity found through it stops showing and it isn't suggested again.
   */
  async #confirmRemove(feed: MemberFeed) {
    const index = this._feeds.findIndex((f) => f.id === feed.id);
    try {
      await MemberFeedsService.remove(feed.id, this._removeReasonDraft.trim() || undefined);
      this._feeds = this._feeds.filter((f) => f.id !== feed.id);
      this._removingId = null;
      this._removeReasonDraft = "";
      this._errorMessage = "";
      this._statusMessage = `${feed.platform} was removed from your profile.`;

      // The row is gone, so put focus on the link that took its place (or the add form) rather than losing it.
      await this.updateComplete;
      const next = this._feeds[Math.min(index, this._feeds.length - 1)];
      const target =
        (next && this.querySelector<HTMLElement>(`[data-feed-id="${next.id}"] [data-action="edit"]`)) ??
        this.querySelector<HTMLElement>("#dc-feed-manager-platform");
      target?.focus();
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Couldn't remove that link.";
    }
  }

  #renderFeed(feed: MemberFeed, group: MemberFeed[]): TemplateResult {
    if (this._removingId === feed.id) {
      return this.#renderRemoveConfirmation(feed);
    }

    if (this._editingId === feed.id) {
      return html`
        <li class="dc-feed-manager__item dc-feed-manager__item--editing" data-feed-id=${feed.id}>
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
          <p class="dc-feed-manager__not-me">
            Not your link?
            <button class="link-action" type="button" data-action="not-me" @click=${() => this.#startRemove(feed)}>
              This isn't me
            </button>
          </p>
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
              ${feed.faviconUrl
                ? html`<img
                    class="dc-feed-manager__favicon"
                    src=${feed.faviconUrl}
                    alt=""
                    width="16"
                    height="16"
                    loading="lazy"
                    @load=${onFaviconLoad}
                    @error=${onFaviconError}
                  />`
                : ""}
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
            <button class="link-action" type="button" data-action="edit" @click=${() => this.#startEdit(feed)}>Edit</button>
            <button class="link-action" type="button" @click=${() => this.#toggleHidden(feed)}>
              ${feed.isHidden ? iconEye : iconEyeOff}
              ${feed.isHidden ? "Show on profile" : "Hide from profile"}
            </button>
          </div>
        </div>
      </li>
    `;
  }

  /**
   * The only way to take a link off: a confirmation in the row that says plainly what happens. Cancel comes first and
   * gets focus, and the confirm button says exactly what it does.
   */
  #renderRemoveConfirmation(feed: MemberFeed): TemplateResult {
    const titleId = `dc-feed-manager-remove-title-${feed.id}`;
    const descriptionId = `dc-feed-manager-remove-description-${feed.id}`;
    const reasonId = `dc-feed-manager-remove-reason-${feed.id}`;

    return html`
      <li class="dc-feed-manager__item dc-feed-manager__item--removing" data-feed-id=${feed.id}>
        <div class="dc-feed-manager__remove" role="group" aria-labelledby=${titleId} aria-describedby=${descriptionId}>
          <p id=${titleId} class="dc-feed-manager__remove-title">Remove <strong>${feed.platform}</strong> from your profile?</p>
          <div id=${descriptionId} class="dc-feed-manager__remove-description">
            <p>
              If <span class="dc-feed-manager__url">${feed.url}</span> isn't yours, we'll remove it from your profile completely:
              the link itself, and anything we found through it, such as posts from that address. We won't suggest it to you
              again.
            </p>
            <p>Changed your mind later? You can add it again yourself with the form below your links.</p>
          </div>
          <label for=${reasonId}>Anything we should know? (optional)</label>
          <textarea
            id=${reasonId}
            rows="2"
            .value=${this._removeReasonDraft}
            @input=${(event: Event) => (this._removeReasonDraft = (event.target as HTMLTextAreaElement).value)}
          ></textarea>
          <div class="dc-feed-manager__item-actions">
            <button class="btn" type="button" data-action="cancel-remove" @click=${() => this.#cancelRemove(feed.id)}>Cancel</button>
            <button class="btn dc-feed-manager__confirm-remove" type="button" data-action="confirm-remove" @click=${() => this.#confirmRemove(feed)}>
              Yes, remove it from my profile
            </button>
          </div>
        </div>
      </li>
    `;
  }

  #renderGroup(title: string, feeds: MemberFeed[]): TemplateResult | string {
    if (feeds.length === 0) return "";

    // Keyed by id, so a reordered row moves with its icon. Unkeyed, Lit kept the rows in place and gave each `<img>`
    // another link's address, which the browser can request again (it does with its cache off or expired).

    return html`
      <div class="dc-feed-manager__group">
        <h3 class="dc-feed-manager__group-title">${title}</h3>
        <ul class="dc-feed-manager__list">
          ${repeat(
            feeds,
            (feed) => feed.id,
            (feed) => this.#renderFeed(feed, feeds)
          )}
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
              Hiding a feed keeps it here for you to manage, but takes it off your public profile. Drag a link by its handle,
              or use the up and down buttons, to change the order it appears in. If a link isn't yours, choose Edit, then
              "This isn't me".
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
