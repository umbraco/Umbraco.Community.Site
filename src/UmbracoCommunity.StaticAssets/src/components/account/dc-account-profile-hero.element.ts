import { render } from "lit";
import { customElement } from "lit/decorators.js";
import { MEMBER_FEEDS_CHANGED_EVENT, MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service.js";
import {
  PROFILE_DETAILS_CHANGED_EVENT,
  PROFILE_VISIBILITY_CHANGED_EVENT,
  type ProfileDetails,
  type ProfileVisibilityChangedDetail,
} from "../../services/profile-onboarding.service.js";
import {
  PROFILE_BADGES_CHANGED_EVENT,
  badgeKind,
  type ProfileBadge,
  type ProfileBadgesChangedDetail,
} from "../../services/profile-badges.service.js";
import { iconAward } from "../../svg/lucide-icons.js";
import { watchFavicon } from "../../util/favicon.js";

const elementName = "dc-account-profile-hero";

/** The parts of the server-rendered hero (Views/Partials/Profiles/_ProfileHero.cshtml, with Live set) this element updates. */
type HeroSlot = "name" | "headline" | "bio" | "links" | "badges" | "hidden-note";

/**
 * The account page's header: a preview of the member's public profile hero, server rendered. Once upgraded it follows
 * the edits made on the page, so the member sees what their profile shows:
 * - a link added, edited, hidden, shown, reordered or removed (including "Add to my links" on a link we found) reloads
 *   the link pills, showing the links that aren't hidden, in their order, as the public profile does;
 * - a saved name, headline or bio replaces the text (an empty headline or bio is hidden, as on the profile);
 * - showing or hiding the profile shows or hides the "Your profile is hidden" note;
 * - choosing which forum badges to show redraws the badge row under the link pills (hidden when none are chosen).
 * A photo change needs nothing here: the photo carries `data-member-avatar`, which the site already keeps up to date.
 * If the links can't be reloaded the pills already shown stay. A pill whose link is still there keeps its icon element, so
 * a reload (after a reorder, say) never loads the icons again; an icon that fails to load is hidden on its own and
 * tried again later (util/favicon.ts).
 *
 * It also adds a slim bar (photo, name, headline and link pills) that stays in view under the site header once the full
 * hero has scrolled away, so the member can watch their changes while working further down the page. The bar is copied
 * from the hero after every update, so the two never disagree. It is a visual copy only: `aria-hidden`, `inert` and with
 * plain-text pills, so nothing is announced or focusable twice. The CSS (css/pages/account-page.css) shows it from --md
 * up only; this element just switches `is-compact` on while the hero is above the bar.
 */
@customElement(elementName)
export class AccountProfileHeroElement extends HTMLElement {
  /** Counts link reloads, so an answer that arrives after a newer one started is ignored. */
  #linksRequest = 0;

  /** The compact bar (see above), or null when the hero isn't there. */
  #bar: HTMLElement | null = null;
  #heroObserver: IntersectionObserver | null = null;
  #wideScreen: MediaQueryList | null = null;

  /** The rootMargin depends on the header's height, which differs between small and large screens. */
  readonly #onScreenChange = () => this.#observeHero();

  readonly #onFeedsChanged = () => void this.refreshLinks();

  readonly #onDetailsChanged = (event: Event) => {
    const details = (event as CustomEvent<ProfileDetails>).detail;
    if (details) this.#applyDetails(details);
  };

  readonly #onBadgesChanged = (event: Event) => {
    const detail = (event as CustomEvent<ProfileBadgesChangedDetail>).detail;
    if (detail) this.#renderBadges(detail.shown);
  };

  readonly #onVisibilityChanged = (event: Event) => {
    const detail = (event as CustomEvent<ProfileVisibilityChangedDetail>).detail;
    const note = this.#slot("hidden-note");
    if (note && detail) note.hidden = detail.visible;
  };

  connectedCallback() {
    window.addEventListener(MEMBER_FEEDS_CHANGED_EVENT, this.#onFeedsChanged);
    window.addEventListener(PROFILE_DETAILS_CHANGED_EVENT, this.#onDetailsChanged);
    window.addEventListener(PROFILE_VISIBILITY_CHANGED_EVENT, this.#onVisibilityChanged);
    window.addEventListener(PROFILE_BADGES_CHANGED_EVENT, this.#onBadgesChanged);

    this.#slot("links")?.querySelectorAll<HTMLImageElement>("img").forEach(watchFavicon);

    const hero = this.#hero();
    if (hero && !this.#bar) {
      this.#bar = this.#createBar();
      hero.after(this.#bar);
      this.#syncBar();
      this.#observeHero();
      this.#wideScreen = window.matchMedia?.("(min-width: 1024px)") ?? null;
      this.#wideScreen?.addEventListener("change", this.#onScreenChange);
    }
  }

  disconnectedCallback() {
    window.removeEventListener(MEMBER_FEEDS_CHANGED_EVENT, this.#onFeedsChanged);
    window.removeEventListener(PROFILE_DETAILS_CHANGED_EVENT, this.#onDetailsChanged);
    window.removeEventListener(PROFILE_VISIBILITY_CHANGED_EVENT, this.#onVisibilityChanged);
    window.removeEventListener(PROFILE_BADGES_CHANGED_EVENT, this.#onBadgesChanged);

    this.#heroObserver?.disconnect();
    this.#heroObserver = null;
    this.#wideScreen?.removeEventListener("change", this.#onScreenChange);
    this.#wideScreen = null;
    this.#bar?.remove();
    this.#bar = null;
  }

  /** Whether the compact bar is showing (the full hero has scrolled up past it). */
  get isCompact(): boolean {
    return this.#bar?.classList.contains("is-compact") ?? false;
  }

  /** Reloads the member's links and shows the ones on their profile as pills. */
  async refreshLinks(): Promise<void> {
    const request = ++this.#linksRequest;
    let feeds: MemberFeed[];
    try {
      feeds = await MemberFeedsService.list();
    } catch {
      // Keep the pills already shown; the feed manager reports the problem.
      return;
    }
    if (request !== this.#linksRequest) return;
    this.#renderLinks(feeds.filter((feed) => !feed.isHidden));
  }

  #slot(name: HeroSlot): HTMLElement | null {
    return this.querySelector<HTMLElement>(`[data-profile-hero="${name}"]`);
  }

  #hero(): HTMLElement | null {
    return this.querySelector<HTMLElement>(".community-profile__hero");
  }

  /**
   * The bar is a zero-height sticky box (its band hangs above it), stuck at the bottom edge of where the band shows. It
   * goes compact once the hero's bottom passes that edge, which is the moment the box starts to stick.
   */
  #observeHero() {
    this.#heroObserver?.disconnect();
    this.#heroObserver = null;
    const hero = this.#hero();
    const bar = this.#bar;
    if (!hero || !bar || typeof IntersectionObserver === "undefined") return;

    const stuckAt = parseFloat(getComputedStyle(bar).top) || 0;
    this.#heroObserver = new IntersectionObserver(
      ([entry]) => {
        if (!entry) return;
        const edge = entry.rootBounds?.top ?? stuckAt;
        bar.classList.toggle("is-compact", !entry.isIntersecting && entry.boundingClientRect.bottom <= edge);
      },
      { rootMargin: `-${Math.round(stuckAt)}px 0px 0px 0px` }
    );
    this.#heroObserver.observe(hero);
  }

  #createBar(): HTMLElement {
    const bar = document.createElement("div");
    bar.className = "account-profile-bar";
    bar.setAttribute("aria-hidden", "true");
    bar.setAttribute("inert", "");
    bar.innerHTML = `
      <div class="account-profile-bar__band">
        <div class="account-profile-bar__inner">
          <img class="account-profile-bar__avatar" alt="" width="40" height="40" data-member-avatar hidden />
          <p class="account-profile-bar__identity">
            <span class="account-profile-bar__name"></span>
            <span class="account-profile-bar__headline"></span>
          </p>
          <ul class="account-profile-bar__links"></ul>
        </div>
      </div>`;
    return bar;
  }

  /** Copies the hero's photo, name, headline and link pills into the compact bar. */
  #syncBar() {
    const bar = this.#bar;
    const hero = this.#hero();
    if (!bar || !hero) return;

    const heroAvatar = hero.querySelector<HTMLImageElement>("img.community-profile__avatar");
    const avatar = bar.querySelector<HTMLImageElement>(".account-profile-bar__avatar")!;
    avatar.hidden = !heroAvatar;
    if (heroAvatar) {
      avatar.src = heroAvatar.getAttribute("src") ?? "";
      avatar.classList.toggle("account-profile-bar__avatar--mvp", heroAvatar.classList.contains("community-profile__avatar--mvp"));
    }

    bar.querySelector(".account-profile-bar__name")!.textContent = this.#slot("name")?.textContent?.trim() ?? "";

    const heroHeadline = this.#slot("headline");
    const headline = bar.querySelector<HTMLElement>(".account-profile-bar__headline")!;
    headline.textContent = heroHeadline && !heroHeadline.hidden ? (heroHeadline.textContent?.trim() ?? "") : "";
    headline.hidden = !headline.textContent;

    const heroLinks = this.#slot("links");
    const links = bar.querySelector<HTMLElement>(".account-profile-bar__links")!;
    const anchors = heroLinks && !heroLinks.hidden ? [...heroLinks.querySelectorAll("a")] : [];
    links.replaceChildren(
      ...anchors.map((anchor) => {
        // Plain text, not links: the hero's pills are the ones to use.
        const item = document.createElement("li");
        const favicon = anchor.querySelector("img");
        if (favicon?.getAttribute("src")) {
          item.append(this.#createIcon(favicon.getAttribute("src")!, "eager"));
        }
        item.append(anchor.textContent?.trim() ?? "");
        return item;
      })
    );
    links.hidden = anchors.length === 0;
  }

  #applyDetails(details: ProfileDetails) {
    const name = this.#slot("name");
    if (name && details.displayName) {
      name.textContent = details.displayName;
      // The photo's alt text is the member's name, as on the profile.
      this.#hero()?.querySelector<HTMLImageElement>("img[data-member-avatar]")?.setAttribute("alt", details.displayName);
    }
    this.#setOptionalText("headline", details.headline);
    this.#setOptionalText("bio", details.bio);
    this.#syncBar();
  }

  #setOptionalText(name: HeroSlot, value: string | null) {
    const element = this.#slot(name);
    if (!element) return;
    element.textContent = value ?? "";
    element.hidden = !value;
  }

  /** A pill's icon, as the profile hero renders it, hidden on its own (and tried again) if it fails to load. */
  #createIcon(src: string, loading: "lazy" | "eager" = "lazy"): HTMLImageElement {
    const icon = document.createElement("img");
    icon.className = "community-profile__social-favicon";
    icon.alt = "";
    icon.width = 16;
    icon.height = 16;
    icon.loading = loading;
    watchFavicon(icon);
    icon.src = src;
    return icon;
  }

  /**
   * The same markup as the profile hero's badge row (Views/Partials/Profiles/_ProfileHero.cshtml). The compact bar
   * doesn't show badges, so it needs no update.
   */
  #renderBadges(badges: ProfileBadge[]) {
    const list = this.#slot("badges");
    if (!list) return;

    list.replaceChildren(
      ...badges.map((badge) => {
        // A badge with a page on the forum is a link to it, opened like the link pills; otherwise a plain chip.
        let chip: HTMLElement;
        if (badge.url) {
          const link = document.createElement("a");
          link.href = badge.url;
          link.target = "_blank";
          link.rel = "noopener noreferrer";
          link.className = "no-link-animation";
          chip = link;
        } else {
          chip = document.createElement("span");
        }
        const type = badge.badgeType?.toLowerCase();
        chip.classList.add("community-profile__forum-badge");
        if (type === "gold" || type === "silver" || type === "bronze") {
          chip.classList.add(`community-profile__forum-badge--${type}`);
        }
        const kind = badgeKind(badge);
        chip.title = badge.description ? `${kind}: ${badge.description}` : kind;
        const item = document.createElement("li");
        item.append(chip);

        if (badge.iconUrl) {
          const icon = document.createElement("img");
          icon.className = "community-profile__forum-badge-icon";
          icon.alt = "";
          icon.width = 16;
          icon.height = 16;
          icon.loading = "lazy";
          icon.src = badge.iconUrl;
          chip.append(icon);
        } else {
          const holder = document.createElement("span");
          render(iconAward, holder);
          const svg = holder.querySelector("svg");
          if (svg) chip.append(svg);
        }

        const name = document.createElement("span");
        name.className = "community-profile__forum-badge-name";
        name.textContent = badge.name;
        const kindText = document.createElement("span");
        kindText.className = "sr-only";
        kindText.textContent = `(${kind})`;
        chip.append(name, kindText);
        return item;
      })
    );
    list.hidden = badges.length === 0;
  }

  /** The same markup as the profile hero's pills. */
  #renderLinks(feeds: MemberFeed[]) {
    const list = this.#slot("links");
    if (!list) return;

    // The icons already shown, by link address, to move into the new pills rather than load again. One that failed
    // (hidden) is made afresh instead, so it gets another chance.
    const shownIcons = new Map<string, HTMLImageElement>();
    for (const anchor of list.querySelectorAll("a")) {
      const icon = anchor.querySelector("img");
      const href = anchor.getAttribute("href");
      if (icon && !icon.hidden && href) shownIcons.set(href, icon);
    }

    list.replaceChildren(
      ...feeds.map((feed) => {
        const link = document.createElement("a");
        link.className = "no-link-animation";
        link.href = feed.url;
        link.rel = "me noopener";
        link.target = "_blank";
        if (feed.faviconUrl) {
          link.append(shownIcons.get(feed.url) ?? this.#createIcon(feed.faviconUrl));
        }
        link.append(feed.platform);

        const item = document.createElement("li");
        item.append(link);
        return item;
      })
    );
    list.hidden = feeds.length === 0;
    this.#syncBar();
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: AccountProfileHeroElement;
  }
}
