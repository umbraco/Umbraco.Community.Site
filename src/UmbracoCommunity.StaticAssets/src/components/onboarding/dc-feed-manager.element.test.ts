import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { FeedManagerElement } from "./dc-feed-manager.element";
import { MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service";

if (!customElements.get("dc-feed-manager")) {
  customElements.define("dc-feed-manager", FeedManagerElement);
}

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

async function mount(feeds: MemberFeed[]): Promise<FeedManagerElement> {
  vi.spyOn(MemberFeedsService, "list").mockResolvedValue(feeds);
  const element = new FeedManagerElement();
  document.body.appendChild(element);
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await element.updateComplete;
  }
  return element;
}

describe("dc-feed-manager", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows whether each link was checked as the member's", async () => {
    const element = await mount([
      feed({ id: 1, matchStatus: "Confirmed" }),
      feed({ id: 2, url: "https://two.example", matchStatus: "Held" }),
      feed({ id: 3, url: "https://three.example", matchStatus: "Rejected" }),
    ]);

    const labels = Array.from(element.querySelectorAll(".dc-feed-manager__match")).map((e) => e.textContent?.trim());
    expect(labels).toEqual(["Matches your profile", "Waiting for a check", "Marked as not yours"]);
  });

  it("shows no match status for links without one", async () => {
    const element = await mount([feed({ matchStatus: null }), feed({ id: 2, url: "https://two.example" })]);

    expect(element.querySelector(".dc-feed-manager__match")).toBeNull();
  });
});
