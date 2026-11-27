import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { LinkSuggestionsElement } from "./dc-link-suggestions.element";
import { ProfileLinksService, type LinkSuggestion } from "../../services/profile-links.service";
import { MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service";
import { ServiceBase } from "../../services/service-base";
import { FeedManagerElement } from "./dc-feed-manager.element";

if (!customElements.get("dc-link-suggestions")) {
  customElements.define("dc-link-suggestions", LinkSuggestionsElement);
}
if (!customElements.get("dc-feed-manager")) {
  customElements.define("dc-feed-manager", FeedManagerElement);
}

const account: LinkSuggestion = { url: "https://social.example/@octocat", platform: "Mastodon", name: "octocat", kind: "Account" };
const feed: LinkSuggestion = { url: "https://octo.example/rss", platform: null, name: "Octo's blog", kind: "Feed" };

function memberFeed(overrides: Partial<MemberFeed> = {}): MemberFeed {
  return {
    id: 1,
    platform: "Mastodon",
    url: account.url,
    isHidden: false,
    source: "Member",
    status: "Visible",
    previewTitle: null,
    faviconUrl: null,
    previewImageUrl: null,
    ...overrides,
  };
}

async function settle(element: LinkSuggestionsElement) {
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await element.updateComplete;
  }
}

async function mount(): Promise<LinkSuggestionsElement> {
  const element = new LinkSuggestionsElement();
  document.body.appendChild(element);
  await settle(element);
  return element;
}

function buttonFor(element: LinkSuggestionsElement, label: string): HTMLButtonElement {
  const button = Array.from(element.querySelectorAll("button")).find((b) => b.getAttribute("aria-label") === label);
  if (!button) throw new Error(`No button labelled ${label}`);
  return button;
}

describe("dc-link-suggestions", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("lists each suggestion with its name, platform and address", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account, feed]);

    const element = await mount();

    const items = element.querySelectorAll(".dc-link-suggestions__item");
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain("octocat");
    expect(items[0].textContent).toContain("(Mastodon)");
    expect(items[0].querySelector("a")?.getAttribute("href")).toBe(account.url);
    expect(items[1].textContent).toContain("Octo's blog");
    expect(element.querySelector("ul")?.getAttribute("aria-label")).toBe("Links we found for you");
  });

  it("says so when nothing was found", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([]);

    const element = await mount();

    expect(element.querySelector("ul")).toBeNull();
    expect(element.textContent).toContain("We haven't found any other links for you right now.");
  });

  it("adds a suggestion to the member's links and refreshes the list", async () => {
    const suggestions = vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValueOnce([account, feed]).mockResolvedValueOnce([feed]);
    const add = vi.spyOn(MemberFeedsService, "add").mockResolvedValue(memberFeed());
    const element = await mount();

    buttonFor(element, "Add octocat to my links").click();
    await settle(element);

    expect(add).toHaveBeenCalledWith("Mastodon", account.url);
    expect(suggestions).toHaveBeenCalledTimes(2);
    expect(element.querySelectorAll(".dc-link-suggestions__item")).toHaveLength(1);
    expect(element.querySelector('[role="status"]')?.textContent).toContain("octocat is added to your links");
  });

  it("shows each suggestion's site icon, like the member's own links", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([{ ...account, faviconUrl: "/api/feed-submission/image-proxy?token=abc" }, feed]);

    const element = await mount();

    const items = element.querySelectorAll(".dc-link-suggestions__item");
    const icon = items[0].querySelector<HTMLImageElement>("img.dc-feed-manager__favicon");
    expect(icon?.getAttribute("src")).toBe("/api/feed-submission/image-proxy?token=abc");
    expect(icon?.getAttribute("alt")).toBe("");
    expect(items[1].querySelector("img")).toBeNull();
  });

  it("hides an icon that fails to load", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([{ ...account, faviconUrl: "/missing" }]);
    const element = await mount();

    const icon = element.querySelector<HTMLImageElement>("img.dc-feed-manager__favicon")!;
    icon.dispatchEvent(new Event("error"));

    expect(icon.hidden).toBe(true);
  });

  it("puts an added suggestion in the member's list of links on the same page", async () => {
    const added = memberFeed({ id: 7 });
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValueOnce([account]).mockResolvedValueOnce([]);
    // Through the real MemberFeedsService: the list starts empty, the add answers with the saved link.
    vi.spyOn(ServiceBase, "get").mockResolvedValue(new Response("[]", { status: 200 }));
    vi.spyOn(ServiceBase, "post").mockResolvedValue(new Response(JSON.stringify(added), { status: 200 }));
    const manager = new FeedManagerElement();
    document.body.appendChild(manager);
    const element = await mount();
    await settle(element);
    expect(manager.querySelectorAll("[data-feed-id]")).toHaveLength(0);

    buttonFor(element, "Add octocat to my links").click();
    await settle(element);
    await manager.updateComplete;

    const rows = manager.querySelectorAll<HTMLElement>("[data-feed-id]");
    expect(rows).toHaveLength(1);
    expect(rows[0].dataset.feedId).toBe("7");
    expect(rows[0].textContent).toContain(account.url);
    expect(element.querySelectorAll(".dc-link-suggestions__item")).toHaveLength(0);
  });

  it("keeps the suggestion and shows the error when adding fails", async () => {
    const suggestions = vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account]);
    vi.spyOn(ServiceBase, "post").mockResolvedValue(
      new Response(JSON.stringify({ error: "Start onboarding before adding feeds." }), { status: 400 })
    );
    const changed = vi.fn();
    window.addEventListener("dc-member-feeds-changed", changed);
    const element = await mount();

    buttonFor(element, "Add octocat to my links").click();
    await settle(element);
    window.removeEventListener("dc-member-feeds-changed", changed);

    expect(element.querySelectorAll(".dc-link-suggestions__item")).toHaveLength(1);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Start onboarding before adding feeds.");
    expect(element.querySelector('[role="status"]')?.textContent).toBe("");
    expect(buttonFor(element, "Add octocat to my links").disabled).toBe(false);
    expect(suggestions).toHaveBeenCalledTimes(1);
    expect(changed).not.toHaveBeenCalled();
  });

  it("uses the feed's name as the platform when adding a feed", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([feed]);
    const add = vi.spyOn(MemberFeedsService, "add").mockResolvedValue(memberFeed({ url: feed.url }));
    const element = await mount();

    buttonFor(element, "Add Octo's blog to my links").click();
    await settle(element);

    expect(add).toHaveBeenCalledWith("Octo's blog", feed.url);
  });

  it("asks before marking a suggestion as not the member's, without saving anything yet", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account, feed]);
    const notMe = vi.spyOn(ProfileLinksService, "notMe").mockResolvedValue();
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);

    expect(notMe).not.toHaveBeenCalled();
    const group = element.querySelector<HTMLElement>('[role="group"]')!;
    expect(group).not.toBeNull();
    expect(element.querySelector(`#${group.getAttribute("aria-labelledby")}`)?.textContent).toContain("Is octocat not yours?");
    const description = element.querySelector(`#${group.getAttribute("aria-describedby")}`)?.textContent ?? "";
    expect(description).toContain("anything we found through");
    expect(description).toContain(account.url);
    expect(description).toContain("We won't suggest it again.");
    expect(group.querySelector('[data-action="confirm-not-me"]')?.textContent?.trim()).toBe("Yes, this isn't me");
    expect(document.activeElement).toBe(group.querySelector('[data-action="cancel-not-me"]'));
    // The other suggestion is still listed as usual, and only one confirmation is open.
    expect(element.querySelectorAll('[role="group"]')).toHaveLength(1);
    expect(element.querySelectorAll(".dc-link-suggestions__item")[1].textContent).toContain("Octo's blog");
  });

  it("closes the confirmation on Cancel without saving and puts focus back on the button", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account]);
    const notMe = vi.spyOn(ProfileLinksService, "notMe").mockResolvedValue();
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);
    element.querySelector<HTMLButtonElement>('[data-action="cancel-not-me"]')!.click();
    await settle(element);

    expect(notMe).not.toHaveBeenCalled();
    expect(element.querySelector('[role="group"]')).toBeNull();
    expect(document.activeElement).toBe(buttonFor(element, "octocat is not me"));
  });

  it("opens only one confirmation at a time", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account, feed]);
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);
    buttonFor(element, "Octo's blog is not me").click();
    await settle(element);

    const groups = element.querySelectorAll('[role="group"]');
    expect(groups).toHaveLength(1);
    expect(groups[0].textContent).toContain("Is Octo's blog not yours?");
  });

  it("marks a suggestion as not the member's once confirmed, removes it and announces it", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account, feed]);
    const notMe = vi.spyOn(ProfileLinksService, "notMe").mockResolvedValue();
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);
    element.querySelector<HTMLButtonElement>('[data-action="confirm-not-me"]')!.click();
    await settle(element);

    expect(notMe).toHaveBeenCalledTimes(1);
    expect(notMe).toHaveBeenCalledWith(account.url);
    const items = element.querySelectorAll(".dc-link-suggestions__item");
    expect(items).toHaveLength(1);
    expect(items[0].textContent).toContain("Octo's blog");
    expect(element.querySelector('[role="group"]')).toBeNull();
    const status = element.querySelector('[role="status"]');
    expect(status?.getAttribute("aria-live")).toBe("polite");
    expect(status?.textContent).toContain("marked as not yours");
    expect(document.activeElement).toBe(buttonFor(element, "Octo's blog is not me"));
  });

  it("moves focus to the closing message when the last suggestion is marked as not the member's", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account]);
    vi.spyOn(ProfileLinksService, "notMe").mockResolvedValue();
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);
    element.querySelector<HTMLButtonElement>('[data-action="confirm-not-me"]')!.click();
    await settle(element);

    expect(document.activeElement).toBe(element.querySelector(".dc-link-suggestions__hint"));
  });

  it("keeps the suggestion and shows the error when marking it fails", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account]);
    const notMe = vi.spyOn(ProfileLinksService, "notMe").mockRejectedValue(new Error("That link can't be marked as not yours."));
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);
    element.querySelector<HTMLButtonElement>('[data-action="confirm-not-me"]')!.click();
    await settle(element);

    expect(notMe).toHaveBeenCalledTimes(1);
    expect(element.querySelectorAll(".dc-link-suggestions__item")).toHaveLength(1);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("That link can't be marked as not yours.");
    expect(element.querySelector('[role="status"]')?.textContent).toBe("");
    // The confirmation stays open, so the member can try again or cancel.
    const confirm = element.querySelector<HTMLButtonElement>('[data-action="confirm-not-me"]')!;
    expect(confirm.getAttribute("aria-disabled")).toBe("false");
  });

  it("ignores more clicks while marking it is saving", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account, feed]);
    let resolve!: () => void;
    const notMe = vi.spyOn(ProfileLinksService, "notMe").mockReturnValue(new Promise<void>((r) => (resolve = r)));
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);
    const confirm = element.querySelector<HTMLButtonElement>('[data-action="confirm-not-me"]')!;
    confirm.click();
    await settle(element);
    confirm.click();

    expect(notMe).toHaveBeenCalledTimes(1);
    expect(confirm.getAttribute("aria-disabled")).toBe("true");
    expect(buttonFor(element, "Octo's blog is not me").disabled).toBe(true);
    expect(buttonFor(element, "Add Octo's blog to my links").disabled).toBe(true);

    resolve();
    await settle(element);
  });

  it("shows the error when the suggestions can't be loaded", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockRejectedValue(new Error("Finding links for your profile isn't available to you yet."));

    const element = await mount();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("isn't available to you yet");
    expect(element.textContent).not.toContain("We haven't found any other links");
  });
});
