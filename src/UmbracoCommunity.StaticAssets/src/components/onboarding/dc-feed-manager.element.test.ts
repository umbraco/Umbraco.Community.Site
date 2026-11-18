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

  describe("reordering", () => {
    function rows(element: FeedManagerElement): HTMLElement[] {
      return [...element.querySelectorAll<HTMLElement>("[data-feed-id]")];
    }

    function ids(element: FeedManagerElement): number[] {
      return rows(element).map((row) => Number(row.dataset.feedId));
    }

    function move(row: HTMLElement, direction: "up" | "down"): HTMLButtonElement {
      return row.querySelector<HTMLButtonElement>(`[data-action="${direction}"]`)!;
    }

    async function settle(element: FeedManagerElement) {
      for (let i = 0; i < 3; i++) {
        await new Promise((resolve) => setTimeout(resolve, 0));
        await element.updateComplete;
      }
    }

    function drag(type: string, clientY = 0): Event {
      const event = new Event(type, { bubbles: true, cancelable: true });
      Object.defineProperty(event, "clientY", { value: clientY });
      return event;
    }

    const three = () => [
      feed({ id: 1, platform: "Blog" }),
      feed({ id: 2, platform: "Mastodon", url: "https://two.example" }),
      feed({ id: 3, platform: "Bluesky", url: "https://three.example" }),
    ];

    it("gives each link a grab handle and labelled up and down buttons", async () => {
      const element = await mount(three());

      const first = rows(element)[0];
      expect(first.querySelector(".dc-sortable__handle")?.getAttribute("title")).toBe("Drag to reorder");
      expect(move(first, "up").getAttribute("aria-label")).toBe("Move Blog up");
      expect(move(first, "up").disabled).toBe(true);
      expect(move(rows(element)[2], "down").disabled).toBe(true);
    });

    it("moves a link with the buttons, keeps focus on it and announces where it went", async () => {
      const reorder = vi.spyOn(MemberFeedsService, "reorder").mockResolvedValue();
      const element = await mount(three());

      move(rows(element)[2], "up").click();
      await settle(element);

      expect(reorder).toHaveBeenCalledWith([1, 3, 2]);
      expect(ids(element)).toEqual([1, 3, 2]);
      expect(document.activeElement).toBe(move(rows(element)[1], "up"));
      expect(element.querySelector('[role="status"]')?.textContent).toContain("Bluesky moved to position 2 of 3.");
    });

    it("moves a link by drag and drop, taking the place of the row it is dragged over", async () => {
      const reorder = vi.spyOn(MemberFeedsService, "reorder").mockResolvedValue();
      const element = await mount(three());

      rows(element)[2].querySelector(".dc-sortable__handle")!.dispatchEvent(drag("dragstart"));
      await settle(element);
      expect(rows(element)[2].classList.contains("is-dragging")).toBe(true);

      // Dragged up over the first row: it takes that row's place.
      rows(element)[0].dispatchEvent(drag("dragover", -1));
      await settle(element);

      rows(element)[0].dispatchEvent(drag("drop", -1));
      await settle(element);

      expect(reorder).toHaveBeenCalledWith([3, 1, 2]);
      expect(ids(element)).toEqual([3, 1, 2]);
    });

    it("doesn't drop a link into the other group", async () => {
      const reorder = vi.spyOn(MemberFeedsService, "reorder").mockResolvedValue();
      const element = await mount([
        feed({ id: 1, source: "Platform" }),
        feed({ id: 2, source: "Platform", url: "https://two.example" }),
        feed({ id: 3, url: "https://three.example" }),
        feed({ id: 4, url: "https://four.example" }),
      ]);

      rows(element)[3].querySelector(".dc-sortable__handle")!.dispatchEvent(drag("dragstart"));
      rows(element)[0].dispatchEvent(drag("dragover", 0));
      rows(element)[0].dispatchEvent(drag("drop", 0));
      await settle(element);

      expect(reorder).not.toHaveBeenCalled();
      expect(ids(element)).toEqual([1, 2, 3, 4]);
    });

    it("puts the order back and says so when saving it fails", async () => {
      vi.spyOn(MemberFeedsService, "reorder").mockRejectedValue(new Error("Couldn't save the new order."));
      const element = await mount(three());

      move(rows(element)[0], "down").click();
      await settle(element);

      expect(ids(element)).toEqual([1, 2, 3]);
      expect(element.querySelector('[role="alert"]')?.textContent).toContain("Couldn't save the new order.");
    });
  });
});
