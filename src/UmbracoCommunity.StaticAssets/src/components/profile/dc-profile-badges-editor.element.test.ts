import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileBadgesEditorElement } from "./dc-profile-badges-editor.element";
import {
  PROFILE_BADGES_CHANGED_EVENT,
  ProfileBadgesService,
  type ProfileBadge,
  type ProfileBadges,
  type ProfileBadgesChangedDetail,
} from "../../services/profile-badges.service";
import { ServiceBase } from "../../services/service-base";
import { MEMBER_FEEDS_CHANGED_EVENT } from "../../services/member-feeds.service";

if (!customElements.get("dc-profile-badges-editor")) {
  customElements.define("dc-profile-badges-editor", ProfileBadgesEditorElement);
}

function badge(overrides: Partial<ProfileBadge> = {}): ProfileBadge {
  return {
    id: 1,
    name: "Umbraco MVP",
    description: "An Umbraco MVP",
    iconUrl: "/api/feed-submission/image-proxy?token=mvp",
    badgeType: "Gold",
    grantCount: 113,
    grantedAt: "2025-06-01T00:00:00Z",
    shown: false,
    url: "https://forum.umbraco.com/badges/1/umbraco-mvp",
    ...overrides,
  };
}

function badges(overrides: Partial<ProfileBadges> = {}): ProfileBadges {
  return {
    status: "ok",
    forumUsername: "octocat",
    forumProfileUrl: "https://forum.umbraco.com/u/octocat",
    badges: [badge(), badge({ id: 2, name: "Member", description: null, iconUrl: null, badgeType: "Bronze", grantCount: 1, url: null })],
    shownIds: [],
    maxShown: 8,
    ...overrides,
  };
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

async function settle(element: ProfileBadgesEditorElement) {
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await element.updateComplete;
  }
}

async function mount(): Promise<ProfileBadgesEditorElement> {
  const element = new ProfileBadgesEditorElement();
  document.body.appendChild(element);
  await settle(element);
  return element;
}

function checkbox(element: ProfileBadgesEditorElement, name: string): HTMLInputElement {
  const label = [...element.querySelectorAll("label")].find((l) => l.textContent?.includes(name))!;
  return element.querySelector<HTMLInputElement>(`#${label.htmlFor}`)!;
}

async function click(element: ProfileBadgesEditorElement, input: HTMLInputElement) {
  input.click();
  await settle(element);
}

describe("dc-profile-badges-editor", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("lists each badge with a labelled checkbox, its kind and description", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges()));
    const element = await mount();

    const mvp = checkbox(element, "Umbraco MVP");
    expect(mvp.type).toBe("checkbox");
    expect(mvp.checked).toBe(false);
    const hint = element.querySelector(`#${mvp.getAttribute("aria-describedby")}`)!.textContent!;
    expect(hint).toContain("Gold forum badge");
    expect(hint).toContain("113 members have it");
    expect(hint).toContain("An Umbraco MVP");
    expect(element.querySelector<HTMLImageElement>("img.dc-profile-badges-editor__icon")?.getAttribute("src")).toBe(
      "/api/feed-submission/image-proxy?token=mvp"
    );
    // A badge with no picture gets the built-in icon, nothing from the forum.
    expect(element.querySelector(".dc-profile-badges-editor__icon--generic svg")).not.toBeNull();
    expect(element.querySelector("legend")?.textContent).toContain("0 of 8 chosen");

    // The badge's forum page, outside the label so the checkbox's name stays the badge name.
    const links = [...element.querySelectorAll<HTMLAnchorElement>("a.dc-profile-badges-editor__link")];
    expect(links.map((a) => a.getAttribute("href"))).toEqual(["https://forum.umbraco.com/badges/1/umbraco-mvp"]);
    expect(links[0].target).toBe("_blank");
    expect(links[0].rel).toBe("noopener noreferrer");
    expect(links[0].closest("label")).toBeNull();
  });

  it("saves a ticked badge, announces it and tells the page", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges()));
    const saved = badges({ badges: [badge({ shown: true }), badges().badges[1]], shownIds: [1] });
    const put = vi.spyOn(ServiceBase, "put").mockResolvedValue(json(saved));
    const events: ProfileBadgesChangedDetail[] = [];
    const listener = (event: Event) => events.push((event as CustomEvent<ProfileBadgesChangedDetail>).detail);
    window.addEventListener(PROFILE_BADGES_CHANGED_EVENT, listener);

    try {
      const element = await mount();
      await click(element, checkbox(element, "Umbraco MVP"));

      expect(put).toHaveBeenCalledWith("/api/profile/badges", { badgeIds: [1] });
      expect(checkbox(element, "Umbraco MVP").checked).toBe(true);
      expect(element.querySelector('[role="status"]')?.textContent).toContain("Umbraco MVP now shows on your profile.");
      expect(element.querySelector("legend")?.textContent).toContain("1 of 8 chosen");
      expect(events).toEqual([{ shown: [saved.badges[0]] }]);
    } finally {
      window.removeEventListener(PROFILE_BADGES_CHANGED_EVENT, listener);
    }
  });

  it("moves the checkbox back and reports the error when saving fails", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges()));
    vi.spyOn(ServiceBase, "put").mockResolvedValue(json({ error: "We couldn't reach the forum just now." }, 503));
    const element = await mount();

    await click(element, checkbox(element, "Umbraco MVP"));

    expect(checkbox(element, "Umbraco MVP").checked).toBe(false);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("We couldn't reach the forum just now.");
  });

  it("refuses one badge more than the cap without saving", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(
      json(badges({ maxShown: 1, badges: [badge({ shown: true }), badges().badges[1]], shownIds: [1] }))
    );
    const save = vi.spyOn(ProfileBadgesService, "setShown");
    const element = await mount();

    await click(element, checkbox(element, "Member"));

    expect(save).not.toHaveBeenCalled();
    expect(checkbox(element, "Member").checked).toBe(false);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("You can show up to 1 badges");
  });

  it("explains how to add a forum link when the member has none", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(
      json(badges({ status: "noForumLink", forumUsername: null, forumProfileUrl: null, badges: [] }))
    );
    const element = await mount();

    expect(element.textContent).toContain("add the link to your profile on the Umbraco forum");
    expect(element.querySelector<HTMLAnchorElement>('a[href="#feeds"]')?.textContent).toContain("Feeds & links");
    expect(element.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it("says so when the forum has no user by the name in the link", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges({ status: "notFound", badges: [] })));
    const element = await mount();

    expect(element.textContent).toContain("We couldn't find a forum user called @octocat");
    expect(element.querySelector('a[href="#feeds"]')).not.toBeNull();
    expect(element.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it("reloads when the member's links change and tells the header which badges now show", async () => {
    const get = vi.spyOn(ServiceBase, "get").mockResolvedValue(
      json(badges({ status: "noForumLink", forumUsername: null, forumProfileUrl: null, badges: [] }))
    );
    const element = await mount();
    const events: ProfileBadgesChangedDetail[] = [];
    const listener = (event: Event) => events.push((event as CustomEvent<ProfileBadgesChangedDetail>).detail);
    window.addEventListener(PROFILE_BADGES_CHANGED_EVENT, listener);

    try {
      // The member adds their forum link; a badge they chose earlier for that account still holds.
      const loaded = badges({ badges: [badge({ shown: true }), badges().badges[1]], shownIds: [1] });
      get.mockResolvedValue(json(loaded));
      window.dispatchEvent(new CustomEvent(MEMBER_FEEDS_CHANGED_EVENT, { detail: { change: "added" } }));
      await settle(element);

      expect(checkbox(element, "Umbraco MVP").checked).toBe(true);
      expect(events).toEqual([{ shown: [loaded.badges[0]] }]);

      // The link is removed again: nothing shows any more.
      get.mockResolvedValue(json(badges({ status: "noForumLink", forumUsername: null, forumProfileUrl: null, badges: [] })));
      window.dispatchEvent(new CustomEvent(MEMBER_FEEDS_CHANGED_EVENT, { detail: { change: "removed" } }));
      await settle(element);

      expect(events.at(-1)).toEqual({ shown: [] });
    } finally {
      window.removeEventListener(PROFILE_BADGES_CHANGED_EVENT, listener);
    }
  });

  it("leaves the header alone when the forum can't be reached after a link change", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges({ status: "unavailable", badges: [] })));
    const element = await mount();
    const listener = vi.fn();
    window.addEventListener(PROFILE_BADGES_CHANGED_EVENT, listener);

    try {
      window.dispatchEvent(new CustomEvent(MEMBER_FEEDS_CHANGED_EVENT, { detail: { change: "updated" } }));
      await settle(element);

      expect(listener).not.toHaveBeenCalled();
    } finally {
      window.removeEventListener(PROFILE_BADGES_CHANGED_EVENT, listener);
    }
  });

  it("says so when the member has no badges yet", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges({ badges: [] })));
    const element = await mount();

    expect(element.textContent).toContain("You have no badges on the forum yet.");
    expect(element.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it("offers to try again when the forum can't be reached", async () => {
    const get = vi.spyOn(ServiceBase, "get").mockResolvedValue(json(badges({ status: "unavailable", badges: [] })));
    const element = await mount();
    expect(element.textContent).toContain("We couldn't reach the forum just now.");

    get.mockResolvedValue(json(badges()));
    element.querySelector<HTMLButtonElement>("button")!.click();
    await settle(element);

    expect(checkbox(element, "Umbraco MVP")).not.toBeNull();
  });

  it("reports a failed load", async () => {
    vi.spyOn(ServiceBase, "get").mockResolvedValue(json({ error: "Finish setting up your profile first." }, 400));
    const element = await mount();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Finish setting up your profile first.");
  });
});
