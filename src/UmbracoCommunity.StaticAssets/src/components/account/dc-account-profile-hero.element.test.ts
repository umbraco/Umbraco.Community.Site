import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { AccountProfileHeroElement } from "./dc-account-profile-hero.element";
import { MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service";
import { ProfileOnboardingService, type ProfileDetails } from "../../services/profile-onboarding.service";
import { ServiceBase } from "../../services/service-base";
import { ProfileBadgesService, type ProfileBadge, type ProfileBadges } from "../../services/profile-badges.service";
import { FAVICON_RETRY_DELAYS } from "../../util/favicon";

if (!customElements.get("dc-account-profile-hero")) {
  customElements.define("dc-account-profile-hero", AccountProfileHeroElement);
}

// What Views/Partials/Profiles/_ProfileHero.cshtml renders on the account page (Live), for a member with no headline.
const markup = `
  <dc-account-profile-hero>
    <header class="community-profile__hero">
      <img class="community-profile__avatar" src="/avatar.png" alt="The Octocat" data-member-avatar />
      <h1 class="community-profile__name"><span data-profile-hero="name">The Octocat</span></h1>
      <p class="community-profile__headline" data-profile-hero="headline" hidden></p>
      <p class="community-profile__bio" data-profile-hero="bio">Hello</p>
      <ul class="community-profile__socials" data-profile-hero="links">
        <li><a class="no-link-animation" href="https://octo.example">Blog</a></li>
      </ul>
      <ul class="community-profile__badges-row" data-profile-hero="badges" aria-label="Forum badges" hidden></ul>
      <p data-profile-hero="hidden-note">Your profile is hidden</p>
    </header>
  </dc-account-profile-hero>
`;

function feed(overrides: Partial<MemberFeed> = {}): MemberFeed {
  return {
    id: 1,
    platform: "Blog",
    url: "https://octo.example",
    isHidden: false,
    source: "Member",
    status: "Visible",
    previewTitle: null,
    faviconUrl: null,
    previewImageUrl: null,
    ...overrides,
  };
}

function details(overrides: Partial<ProfileDetails> = {}): ProfileDetails {
  return {
    displayName: "The Octocat",
    headline: null,
    bio: "Hello",
    ...overrides,
  };
}

async function settle() {
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
}

function slot(name: string): HTMLElement {
  return document.querySelector<HTMLElement>(`[data-profile-hero="${name}"]`)!;
}

function pills(): { text: string; href: string | null; icon: string | null }[] {
  return [...slot("links").querySelectorAll("a")].map((a) => ({
    text: a.textContent?.trim() ?? "",
    href: a.getAttribute("href"),
    icon: a.querySelector("img")?.getAttribute("src") ?? null,
  }));
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

describe("dc-account-profile-hero", () => {
  beforeEach(() => {
    document.body.innerHTML = markup;
  });

  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("shows a link added on the page as a pill, as the public profile does", async () => {
    const added = feed({ id: 2, platform: "Mastodon", url: "https://social.example/@octocat", faviconUrl: "/proxy?token=m" });
    vi.spyOn(ServiceBase, "post").mockResolvedValue(json(added));
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json([feed(), added]));

    await MemberFeedsService.add("Mastodon", added.url);
    await settle();

    expect(pills()).toEqual([
      { text: "Blog", href: "https://octo.example", icon: null },
      { text: "Mastodon", href: "https://social.example/@octocat", icon: "/proxy?token=m" },
    ]);
    const link = slot("links").querySelectorAll("a")[1];
    expect(link.getAttribute("rel")).toBe("me noopener");
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.querySelector("img")?.getAttribute("alt")).toBe("");
  });

  it("leaves hidden links out and hides the list when none are left", async () => {
    vi.spyOn(ServiceBase, "put").mockResolvedValue(new Response(null, { status: 200 }));
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json([feed({ isHidden: true })]));

    await MemberFeedsService.setHidden(1, true);
    await settle();

    expect(pills()).toEqual([]);
    expect(slot("links").hidden).toBe(true);
  });

  it("keeps the pills shown when the links can't be reloaded", async () => {
    vi.spyOn(ServiceBase, "delete").mockResolvedValue(new Response(null, { status: 200 }));
    vi.spyOn(MemberFeedsService, "list").mockRejectedValue(new Error("Offline"));

    await MemberFeedsService.remove(1);
    await settle();

    expect(pills().map((p) => p.text)).toEqual(["Blog"]);
  });

  it("keeps each pill's icon through a reorder, even when the reload gives the icons new addresses", async () => {
    // Reordering reloads the links. The icons must come along as they are, not be loaded again from new addresses:
    // loading every icon again on each move is what used up the image proxy's rate limit and blanked them all.
    vi.spyOn(ServiceBase, "put").mockResolvedValue(new Response(null, { status: 200 }));
    const blog = feed({ faviconUrl: "/proxy?token=b1" });
    const mastodon = feed({ id: 2, platform: "Mastodon", url: "https://social.example/@octocat", faviconUrl: "/proxy?token=m1" });
    const get = vi.spyOn(ServiceBase, "get").mockResolvedValue(json([blog, mastodon]));
    await MemberFeedsService.reorder([1, 2]);
    await settle();
    const [blogIcon, mastodonIcon] = [...slot("links").querySelectorAll("img")];

    get.mockResolvedValue(json([{ ...mastodon, faviconUrl: "/proxy?token=m2" }, { ...blog, faviconUrl: "/proxy?token=b2" }]));
    await MemberFeedsService.reorder([2, 1]);
    await settle();

    expect(pills()).toEqual([
      { text: "Mastodon", href: "https://social.example/@octocat", icon: "/proxy?token=m1" },
      { text: "Blog", href: "https://octo.example", icon: "/proxy?token=b1" },
    ]);
    expect([...slot("links").querySelectorAll("img")]).toEqual([mastodonIcon, blogIcon]);
    expect(blogIcon.hidden).toBe(false);
    expect(mastodonIcon.hidden).toBe(false);
  });

  it("hides only a pill icon that fails to load, and brings it back when a retry loads", async () => {
    vi.useFakeTimers();
    try {
      vi.spyOn(ServiceBase, "put").mockResolvedValue(new Response(null, { status: 200 }));
      vi.spyOn(ServiceBase, "get").mockResolvedValue(
        json([
          feed({ faviconUrl: "/proxy?token=b" }),
          feed({ id: 2, platform: "Mastodon", url: "https://social.example/@octocat", faviconUrl: "/proxy?token=m" }),
        ])
      );
      await MemberFeedsService.reorder([1, 2]);
      await vi.runOnlyPendingTimersAsync();
      const [blogIcon, mastodonIcon] = [...slot("links").querySelectorAll("img")];

      blogIcon.dispatchEvent(new Event("error"));
      expect(blogIcon.hidden).toBe(true);
      expect(mastodonIcon.hidden).toBe(false);

      await vi.advanceTimersByTimeAsync(FAVICON_RETRY_DELAYS[0]);
      expect(blogIcon.loading).toBe("eager");
      blogIcon.dispatchEvent(new Event("load"));
      expect(blogIcon.hidden).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it("shows the chosen forum badges under the links, and hides the row when none are left", async () => {
    const mvp: ProfileBadge = {
      id: 1,
      name: "Umbraco MVP",
      description: "An Umbraco MVP",
      iconUrl: "/api/feed-submission/image-proxy?token=mvp",
      badgeType: "Gold",
      grantCount: 113,
      grantedAt: null,
      shown: true,
      url: "https://forum.umbraco.com/badges/1/umbraco-mvp",
    };
    const member: ProfileBadge = { ...mvp, id: 2, name: "Member", description: null, iconUrl: null, badgeType: null, url: null };
    const saved = (shown: ProfileBadge[]): ProfileBadges => ({
      status: "ok",
      forumUsername: "octocat",
      forumProfileUrl: "https://forum.umbraco.com/u/octocat",
      badges: shown,
      shownIds: shown.map((b) => b.id),
      maxShown: 8,
    });
    const put = vi.spyOn(ServiceBase, "put").mockResolvedValue(json(saved([mvp, member])));

    await ProfileBadgesService.setShown([1, 2]);

    const row = slot("badges");
    expect(row.hidden).toBe(false);
    const chips = [...row.querySelectorAll<HTMLElement>("li > .community-profile__forum-badge")];
    expect(chips.map((chip) => chip.querySelector(".community-profile__forum-badge-name")?.textContent)).toEqual(["Umbraco MVP", "Member"]);

    // A badge with a forum page is a link to it, opened like the link pills.
    const link = chips[0] as HTMLAnchorElement;
    expect(link.tagName).toBe("A");
    expect(link.getAttribute("href")).toBe("https://forum.umbraco.com/badges/1/umbraco-mvp");
    expect(link.target).toBe("_blank");
    expect(link.rel).toBe("noopener noreferrer");
    expect(link.classList.contains("community-profile__forum-badge--gold")).toBe(true);
    expect(link.title).toBe("Gold forum badge: An Umbraco MVP");
    expect(link.querySelector("img")?.getAttribute("src")).toBe("/api/feed-submission/image-proxy?token=mvp");
    expect(link.querySelector("img")?.getAttribute("alt")).toBe("");
    expect(link.textContent?.replace(/\s+/g, " ").trim()).toBe("Umbraco MVP(Gold forum badge)");
    expect(link.querySelector(".sr-only")?.textContent).toBe("(Gold forum badge)");

    // No forum page: a plain chip. No picture: the built-in icon, nothing loaded from the forum.
    expect(chips[1].tagName).toBe("SPAN");
    expect(chips[1].querySelector("img")).toBeNull();
    expect(chips[1].querySelector("svg")).not.toBeNull();
    expect(chips[1].title).toBe("Forum badge");

    // The compact bar copies the link pills only, never the badges.
    expect(document.querySelector(".account-profile-bar")?.querySelector(".community-profile__forum-badge")).toBeNull();

    put.mockResolvedValue(json(saved([])));
    await ProfileBadgesService.setShown([]);

    expect(row.querySelectorAll("li")).toHaveLength(0);
    expect(row.hidden).toBe(true);
  });

  it("follows a saved name, headline and bio", async () => {
    vi.spyOn(ServiceBase, "put").mockResolvedValue(
      json(details({ displayName: "Mona", headline: "Developer at Octo", bio: null }))
    );

    await ProfileOnboardingService.updateHeadline("Developer at Octo");

    expect(slot("name").textContent).toBe("Mona");
    expect(document.querySelector("img[data-member-avatar]")?.getAttribute("alt")).toBe("Mona");
    expect(slot("headline").textContent).toBe("Developer at Octo");
    expect(slot("headline").hidden).toBe(false);
    expect(slot("bio").hidden).toBe(true);
  });

  it("hides the headline and bio when the member empties them", async () => {
    vi.spyOn(ServiceBase, "put")
      .mockResolvedValueOnce(json(details({ headline: "Developer at Octo", bio: "Hello" })))
      .mockResolvedValueOnce(json(details({ headline: null, bio: "Hello" })))
      .mockResolvedValueOnce(json(details({ headline: null, bio: null })));

    await ProfileOnboardingService.updateHeadline("Developer at Octo");
    expect(slot("headline").hidden).toBe(false);

    await ProfileOnboardingService.updateHeadline("");
    expect(slot("headline").textContent).toBe("");
    expect(slot("headline").hidden).toBe(true);

    await ProfileOnboardingService.updateBio("");
    expect(slot("bio").textContent).toBe("");
    expect(slot("bio").hidden).toBe(true);
  });

  it("shows the hidden note only while the profile is hidden", async () => {
    vi.spyOn(ServiceBase, "put").mockResolvedValue(new Response(null, { status: 200 }));

    await ProfileOnboardingService.setProfileVisible(true);
    expect(slot("hidden-note").hidden).toBe(true);

    await ProfileOnboardingService.setProfileVisible(false);
    expect(slot("hidden-note").hidden).toBe(false);
  });

  it("does nothing when a save fails", async () => {
    vi.spyOn(ServiceBase, "put").mockResolvedValue(json({ error: "Too long" }, 400));

    await expect(ProfileOnboardingService.updateHeadline("x")).rejects.toThrow("Too long");

    expect(slot("headline").hidden).toBe(true);
  });
});

describe("dc-account-profile-hero compact bar", () => {
  let observed: Element[];
  let options: IntersectionObserverInit | undefined;
  let report: (entry: Partial<IntersectionObserverEntry>) => void;

  beforeEach(() => {
    observed = [];
    class FakeIntersectionObserver {
      constructor(callback: IntersectionObserverCallback, init?: IntersectionObserverInit) {
        options = init;
        report = (entry) => callback([entry as IntersectionObserverEntry], this as unknown as IntersectionObserver);
      }
      observe(target: Element) {
        observed.push(target);
      }
      disconnect() {}
    }
    vi.stubGlobal("IntersectionObserver", FakeIntersectionObserver);
    document.body.innerHTML = markup;
  });

  afterEach(() => {
    document.body.innerHTML = "";
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  function bar(): HTMLElement {
    return document.querySelector<HTMLElement>(".account-profile-bar")!;
  }

  function host(): AccountProfileHeroElement {
    return document.querySelector("dc-account-profile-hero")!;
  }

  function heroAt(bottom: number, isIntersecting: boolean): Partial<IntersectionObserverEntry> {
    return {
      isIntersecting,
      boundingClientRect: { bottom } as DOMRectReadOnly,
      rootBounds: { top: 190 } as DOMRectReadOnly,
    };
  }

  it("adds a copy of the hero after it that screen readers and the keyboard skip", () => {
    expect(document.querySelector(".community-profile__hero")!.nextElementSibling).toBe(bar());
    expect(bar().getAttribute("aria-hidden")).toBe("true");
    expect(bar().hasAttribute("inert")).toBe(true);
    // Pills are plain text, so the links can't be tabbed to twice.
    expect(bar().querySelectorAll("a, button, [tabindex]")).toHaveLength(0);
    expect(bar().querySelectorAll("[data-profile-hero]")).toHaveLength(0);

    expect(bar().querySelector(".account-profile-bar__name")!.textContent).toBe("The Octocat");
    expect(bar().querySelector<HTMLElement>(".account-profile-bar__headline")!.hidden).toBe(true);
    expect([...bar().querySelectorAll(".account-profile-bar__links li")].map((li) => li.textContent)).toEqual(["Blog"]);
    expect(bar().querySelector("img")!.getAttribute("src")).toBe("/avatar.png");
    expect(bar().querySelector("img")!.getAttribute("alt")).toBe("");
  });

  it("goes compact once the hero has scrolled up past the bar, and back when it returns", () => {
    expect(observed).toEqual([document.querySelector(".community-profile__hero")]);
    expect(options?.rootMargin).toMatch(/^-\d+px 0px 0px 0px$/);
    expect(host().isCompact).toBe(false);

    report(heroAt(150, false));
    expect(host().isCompact).toBe(true);
    expect(bar().classList.contains("is-compact")).toBe(true);

    report(heroAt(400, true));
    expect(host().isCompact).toBe(false);
  });

  it("stays as it is when the hero is merely partly in view", () => {
    report(heroAt(300, true));
    expect(host().isCompact).toBe(false);
  });

  it("follows the same edits as the hero", async () => {
    const added = feed({ id: 2, platform: "Mastodon", url: "https://social.example/@octocat", faviconUrl: "/proxy?token=m" });
    vi.spyOn(ServiceBase, "post").mockResolvedValue(json(added));
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json([feed(), added]));
    vi.spyOn(ServiceBase, "put").mockResolvedValue(
      json(details({ displayName: "Mona", headline: "Developer at Octo" }))
    );

    await MemberFeedsService.add("Mastodon", added.url);
    await settle();
    await ProfileOnboardingService.updateHeadline("Developer at Octo");

    expect(bar().querySelector(".account-profile-bar__name")!.textContent).toBe("Mona");
    const headline = bar().querySelector<HTMLElement>(".account-profile-bar__headline")!;
    expect(headline.textContent).toBe("Developer at Octo");
    expect(headline.hidden).toBe(false);
    const items = [...bar().querySelectorAll(".account-profile-bar__links li")];
    expect(items.map((li) => li.textContent)).toEqual(["Blog", "Mastodon"]);
    expect(items[1].querySelector("img")?.getAttribute("src")).toBe("/proxy?token=m");
    expect(bar().querySelectorAll("a")).toHaveLength(0);
  });

  it("hides the pills when the member has no links on their profile", async () => {
    vi.spyOn(ServiceBase, "put").mockResolvedValue(new Response(null, { status: 200 }));
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json([feed({ isHidden: true })]));

    await MemberFeedsService.setHidden(1, true);
    await settle();

    expect(bar().querySelector<HTMLElement>(".account-profile-bar__links")!.hidden).toBe(true);
  });

  it("removes the bar when the element goes", () => {
    host().remove();
    expect(document.querySelector(".account-profile-bar")).toBeNull();
  });
});
