import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import { MEMBER_FEEDS_CHANGED_EVENT } from "../../services/member-feeds.service.js";
import {
  ProfileBadgesService,
  badgeKind,
  notifyBadgesChanged,
  shownBadges,
  type ProfileBadge,
  type ProfileBadges,
} from "../../services/profile-badges.service.js";
import { iconAward } from "../../svg/lucide-icons.js";

const elementName = "dc-profile-badges-editor";

let instanceCount = 0;

/**
 * The account page's Badges panel: the member's Umbraco forum badges, each with a checkbox to show it on their profile,
 * under their links. The forum account is the one their own forum link (under Feeds & links) points at; the forum
 * username can differ from their GitHub handle. Without such a link the panel says how to add one. When their links
 * change (dc-member-feeds-changed) the panel reloads, and tells the profile header which badges now show, as a new or
 * removed link can change the account. A change is saved straight away (the checkbox moves at once and moves back if saving fails)
 * and the account page's profile header follows it (PROFILE_BADGES_CHANGED_EVENT). At most `maxShown` badges can be
 * shown: ticking one more is refused with a message, rather than disabling the others, so every checkbox stays
 * reachable. Without a forum user by that name, or with no badges yet, it says so.
 */
@customElement(elementName)
export class ProfileBadgesEditorElement extends LitElement {
  @state()
  private _data: ProfileBadges | null = null;

  @state()
  private _loading = true;

  @state()
  private _loadError = "";

  @state()
  private _saving = false;

  @state()
  private _statusMessage = "";

  @state()
  private _errorMessage = "";

  readonly #idPrefix = `${elementName}-${++instanceCount}`;

  // Render in light DOM so the site's existing styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  /** Reloads after the member's links change, and updates the header's badge row (unless the forum couldn't be asked). */
  readonly #onFeedsChanged = async () => {
    await this.load();
    if (this._data && this._data.status !== "unavailable") {
      notifyBadgesChanged(shownBadges(this._data));
    }
  };

  connectedCallback() {
    super.connectedCallback();
    window.addEventListener(MEMBER_FEEDS_CHANGED_EVENT, this.#onFeedsChanged);
    void this.load();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    window.removeEventListener(MEMBER_FEEDS_CHANGED_EVENT, this.#onFeedsChanged);
  }

  /** Loads (or reloads) the member's badges. */
  async load(): Promise<void> {
    this._loading = true;
    this._loadError = "";
    try {
      this._data = await ProfileBadgesService.load();
      this._statusMessage = "";
      this._errorMessage = "";
    } catch (error) {
      this._data = null;
      this._loadError =
        error instanceof Error && error.message ? error.message : "Couldn't load your badges. Please try again.";
    } finally {
      this._loading = false;
    }
  }

  get #shownCount(): number {
    return this._data?.badges.filter((badge) => badge.shown).length ?? 0;
  }

  async #onChange(event: Event, badge: ProfileBadge) {
    const input = event.target as HTMLInputElement;
    const data = this._data;
    if (!data) return;

    // Ignore further clicks while a save is in flight. The input is not disabled, so a keyboard user keeps focus on it.
    if (this._saving) {
      input.checked = badge.shown;
      return;
    }

    const show = input.checked;
    this._statusMessage = "";
    this._errorMessage = "";

    if (show && this.#shownCount >= data.maxShown) {
      input.checked = false;
      this._errorMessage = `You can show up to ${data.maxShown} badges. Untick one to show another.`;
      return;
    }

    const previous = data;
    const next: ProfileBadges = {
      ...data,
      badges: data.badges.map((b) => (b.id === badge.id ? { ...b, shown: show } : b)),
    };
    this._data = next;
    this._saving = true;

    try {
      this._data = await ProfileBadgesService.setShown(next.badges.filter((b) => b.shown).map((b) => b.id));
      this._statusMessage = show
        ? `${badge.name} now shows on your profile.`
        : `${badge.name} no longer shows on your profile.`;
    } catch (error) {
      this._data = previous;
      input.checked = badge.shown;
      this._errorMessage =
        error instanceof Error && error.message ? error.message : "Couldn't save this change. Please try again.";
    } finally {
      this._saving = false;
    }
  }

  #renderIcon(badge: ProfileBadge): TemplateResult {
    return badge.iconUrl
      ? html`<img class="dc-profile-badges-editor__icon" src=${badge.iconUrl} alt="" width="24" height="24" loading="lazy" />`
      : html`<span class="dc-profile-badges-editor__icon dc-profile-badges-editor__icon--generic">${iconAward}</span>`;
  }

  #renderBadge(badge: ProfileBadge): TemplateResult {
    const inputId = `${this.#idPrefix}-badge-${badge.id}`;
    const hintId = `${inputId}-hint`;
    const kind = badgeKind(badge);
    const held =
      badge.grantCount != null && badge.grantCount > 0
        ? ` · ${badge.grantCount === 1 ? "1 member has it" : `${badge.grantCount} members have it`}`
        : "";
    const type = badge.badgeType?.toLowerCase();

    return html`
      <li class="dc-profile-badges-editor__item${type ? ` dc-profile-badges-editor__item--${type}` : ""}">
        <input
          id=${inputId}
          class="dc-profile-badges-editor__input"
          type="checkbox"
          aria-describedby=${hintId}
          aria-busy=${this._saving ? "true" : "false"}
          .checked=${badge.shown}
          @change=${(event: Event) => this.#onChange(event, badge)}
        />
        <label class="dc-profile-badges-editor__label" for=${inputId}>
          ${this.#renderIcon(badge)}
          <span class="dc-profile-badges-editor__name">${badge.name}</span>
        </label>
        <p id=${hintId} class="dc-profile-badges-editor__hint">
          <span class="dc-profile-badges-editor__kind">${kind}${held}</span>
          ${badge.description ? html`<span class="dc-profile-badges-editor__description">${badge.description}</span>` : nothing}
        </p>
        ${badge.url
          ? html`<a class="dc-profile-badges-editor__link" href=${badge.url} target="_blank" rel="noopener noreferrer"
              >View on the forum<span class="sr-only">: ${badge.name} (opens in a new tab)</span></a
            >`
          : nothing}
      </li>
    `;
  }

  #renderForumLink(data: ProfileBadges, text: string): TemplateResult | typeof nothing {
    return data.forumProfileUrl
      ? html`<a href=${data.forumProfileUrl} target="_blank" rel="noopener noreferrer"
          >${text}<span class="sr-only"> (opens in a new tab)</span></a
        >`
      : nothing;
  }

  #renderBody(): TemplateResult {
    if (this._loading && !this._data) {
      return html`<p class="dc-profile-badges-editor__message" role="status">Loading your forum badges…</p>`;
    }

    const data = this._data;
    if (!data) {
      return html`
        <p class="dc-profile-badges-editor__error" role="alert">${this._loadError}</p>
        <button type="button" class="btn is-transparent" @click=${() => this.load()}>Try again</button>
      `;
    }

    if (data.status === "noForumLink") {
      return html`<p class="dc-profile-badges-editor__message">
        To show your forum badges, add the link to your profile on the Umbraco forum (it looks like
        <code>https://forum.umbraco.com/u/your-name</code>) under <a href="#feeds">Feeds &amp; links</a>.
      </p>`;
    }

    if (data.status === "notFound") {
      return html`<p class="dc-profile-badges-editor__message">
        We couldn't find a forum user called @${data.forumUsername}, so there are no badges to show. Check the link to
        your forum profile under <a href="#feeds">Feeds &amp; links</a>.
      </p>`;
    }

    if (data.status === "unavailable") {
      return html`
        <p class="dc-profile-badges-editor__message" role="status">
          We couldn't reach the forum just now. Your profile keeps showing the badges you chose.
        </p>
        <button type="button" class="btn is-transparent" @click=${() => this.load()}>Try again</button>
      `;
    }

    if (data.badges.length === 0) {
      return html`<p class="dc-profile-badges-editor__message">
        You have no badges on the forum yet. ${this.#renderForumLink(data, "Your forum profile")}
      </p>`;
    }

    return html`
      <fieldset class="dc-profile-badges-editor__fieldset">
        <legend class="dc-profile-badges-editor__legend">
          Show on my profile
          <span class="dc-profile-badges-editor__count">(${this.#shownCount} of ${data.maxShown} chosen)</span>
        </legend>
        <ul class="dc-profile-badges-editor__list">
          ${data.badges.map((badge) => this.#renderBadge(badge))}
        </ul>
      </fieldset>
      <p class="dc-profile-badges-editor__forum">
        Badges of forum user @${data.forumUsername}. ${this.#renderForumLink(data, "See them on the forum")}
      </p>
    `;
  }

  render(): TemplateResult {
    return html`
      <div class="dc-profile-badges-editor">
        ${this.#renderBody()}
        <p class="dc-profile-badges-editor__status" role="status" aria-live="polite">${this._statusMessage}</p>
        ${this._errorMessage
          ? html`<p class="dc-profile-badges-editor__error" role="alert">${this._errorMessage}</p>`
          : nothing}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileBadgesEditorElement;
  }
}
