import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { LinkSuggestionsElement } from "./dc-link-suggestions.element";
import { ProfileLinksService, type LinkSuggestion } from "../../services/profile-links.service";
import { MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service";

if (!customElements.get("dc-link-suggestions")) {
  customElements.define("dc-link-suggestions", LinkSuggestionsElement);
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
    expect(element.querySelector('[role="status"]')?.textContent).toContain("octocat is added to your links.");
  });

  it("uses the feed's name as the platform when adding a feed", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([feed]);
    const add = vi.spyOn(MemberFeedsService, "add").mockResolvedValue(memberFeed({ url: feed.url }));
    const element = await mount();

    buttonFor(element, "Add Octo's blog to my links").click();
    await settle(element);

    expect(add).toHaveBeenCalledWith("Octo's blog", feed.url);
  });

  it("marks a suggestion as not the member's, removes it and announces it", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account, feed]);
    const notMe = vi.spyOn(ProfileLinksService, "notMe").mockResolvedValue();
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);

    expect(notMe).toHaveBeenCalledWith(account.url);
    const items = element.querySelectorAll(".dc-link-suggestions__item");
    expect(items).toHaveLength(1);
    expect(items[0].textContent).toContain("Octo's blog");
    const status = element.querySelector('[role="status"]');
    expect(status?.getAttribute("aria-live")).toBe("polite");
    expect(status?.textContent).toContain("marked as not yours");
  });

  it("keeps the suggestion and shows the error when marking it fails", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockResolvedValue([account]);
    vi.spyOn(ProfileLinksService, "notMe").mockRejectedValue(new Error("That link can't be marked as not yours."));
    const element = await mount();

    buttonFor(element, "octocat is not me").click();
    await settle(element);

    expect(element.querySelectorAll(".dc-link-suggestions__item")).toHaveLength(1);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("That link can't be marked as not yours.");
  });

  it("shows the error when the suggestions can't be loaded", async () => {
    vi.spyOn(ProfileLinksService, "suggestions").mockRejectedValue(new Error("Finding links for your profile isn't available to you yet."));

    const element = await mount();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("isn't available to you yet");
    expect(element.textContent).not.toContain("We haven't found any other links");
  });
});
