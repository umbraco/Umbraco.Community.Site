import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { FeedManagerElement } from "./dc-feed-manager.element";
import { MemberFeedsService, type MemberFeed } from "../../services/member-feeds.service";
import { ServiceBase } from "../../services/service-base";

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

  it("adds a link added elsewhere on the page, once", async () => {
    const element = await mount([feed({ id: 1 })]);
    const added = feed({ id: 2, url: "https://two.example", platform: "Mastodon" });

    window.dispatchEvent(new CustomEvent("dc-member-feeds-changed", { detail: { change: "added", feed: added } }));
    window.dispatchEvent(new CustomEvent("dc-member-feeds-changed", { detail: { change: "added", feed: added } }));
    await element.updateComplete;

    expect([...element.querySelectorAll<HTMLElement>("[data-feed-id]")].map((row) => row.dataset.feedId)).toEqual(["1", "2"]);
  });

  it("shows no match status for links without one", async () => {
    const element = await mount([feed({ matchStatus: null }), feed({ id: 2, url: "https://two.example" })]);

    expect(element.querySelector(".dc-feed-manager__match")).toBeNull();
  });

  describe("This isn't me", () => {
    async function settle(element: FeedManagerElement) {
      for (let i = 0; i < 3; i++) {
        await new Promise((resolve) => setTimeout(resolve, 0));
        await element.updateComplete;
      }
    }

    function button(element: FeedManagerElement, action: string): HTMLButtonElement | null {
      return element.querySelector<HTMLButtonElement>(`[data-action="${action}"]`);
    }

    /** Opens the confirmation for the Blog link the member added (id 1); synced links are listed first. */
    async function openConfirmation(element: FeedManagerElement) {
      element.querySelector<HTMLButtonElement>('[data-feed-id="1"] [data-action="edit"]')!.click();
      await settle(element);
      button(element, "not-me")!.click();
      await settle(element);
    }

    const two = () => [
      feed({ id: 1, platform: "Blog", url: "https://octo.example" }),
      feed({ id: 2, platform: "Mastodon", url: "https://social.example/@octo", source: "Platform" }),
    ];

    it("has no Remove button, and keeps This isn't me out of the row's everyday actions", async () => {
      const element = await mount(two());

      const labels = [...element.querySelectorAll("button")].map((b) => b.textContent?.trim());
      expect(labels).not.toContain("Remove");
      expect(labels).not.toContain("This isn't me");
      expect(button(element, "not-me")).toBeNull();
    });

    it("offers This isn't me in edit mode, for links the member added and links we synced", async () => {
      const element = await mount(two());

      for (const id of [1, 2]) {
        element.querySelector<HTMLButtonElement>(`[data-feed-id="${id}"] [data-action="edit"]`)!.click();
        await settle(element);
        expect(element.querySelector(`[data-feed-id="${id}"] [data-action="not-me"]`)?.textContent?.trim()).toBe("This isn't me");
        element.querySelector<HTMLButtonElement>(`[data-feed-id="${id}"] .btn[type="button"]`)!.click();
        await settle(element);
      }
    });

    it("asks for confirmation, saying the link and what we found through it go, with Cancel focused", async () => {
      const element = await mount(two());

      await openConfirmation(element);

      const group = element.querySelector<HTMLElement>('[role="group"]')!;
      expect(group.getAttribute("aria-labelledby")).toBe("dc-feed-manager-remove-title-1");
      const text = group.textContent!.replace(/\s+/g, " ");
      expect(text).toContain("Remove Blog from your profile?");
      expect(text).toContain("we'll remove it from your profile completely");
      expect(text).toContain("anything we found through it");
      expect(text).toContain("We won't suggest it to you again.");
      expect(text).toContain("You can add it again yourself");
      expect(element.querySelector("textarea")).not.toBeNull();
      expect(button(element, "confirm-remove")?.textContent?.trim()).toBe("Yes, remove it from my profile");
      expect(document.activeElement).toBe(button(element, "cancel-remove"));
    });

    it("does nothing on Cancel", async () => {
      const remove = vi.spyOn(MemberFeedsService, "remove").mockResolvedValue();
      const element = await mount(two());

      await openConfirmation(element);
      button(element, "cancel-remove")!.click();
      await settle(element);

      expect(remove).not.toHaveBeenCalled();
      expect(element.querySelector('[role="group"]')).toBeNull();
      expect(element.querySelectorAll("[data-feed-id]")).toHaveLength(2);
      expect(document.activeElement).toBe(element.querySelector('[data-feed-id="1"] [data-action="edit"]'));
    });

    it("removes the link once on confirm, with the detail given, and tells the page", async () => {
      // The real service sends the request and then tells the page; only the request is stubbed here.
      const request = vi.spyOn(ServiceBase, "delete").mockResolvedValue(new Response(null, { status: 200 }));
      const remove = vi.spyOn(MemberFeedsService, "remove");
      const changed = vi.fn();
      window.addEventListener("dc-member-feeds-changed", changed);
      const element = await mount(two());

      await openConfirmation(element);
      const textarea = element.querySelector("textarea")!;
      textarea.value = "Someone else's blog";
      textarea.dispatchEvent(new Event("input"));
      button(element, "confirm-remove")!.click();
      await settle(element);
      window.removeEventListener("dc-member-feeds-changed", changed);

      expect(remove).toHaveBeenCalledTimes(1);
      expect(remove).toHaveBeenCalledWith(1, "Someone else's blog");
      expect(request).toHaveBeenCalledTimes(1);
      expect(request).toHaveBeenCalledWith("/api/member-feeds/1", { reason: "Someone else's blog" });
      expect([...element.querySelectorAll<HTMLElement>("[data-feed-id]")].map((row) => row.dataset.feedId)).toEqual(["2"]);
      expect(changed).toHaveBeenCalledTimes(1);
      expect((changed.mock.calls[0][0] as CustomEvent).detail).toEqual({ change: "removed", feed: undefined });
      expect(element.querySelector('[role="status"]')?.textContent).toContain("Blog was removed from your profile.");
      expect(document.activeElement).toBe(element.querySelector('[data-feed-id="2"] [data-action="edit"]'));
    });

    it("keeps the link and shows the error when removing fails", async () => {
      vi.spyOn(MemberFeedsService, "remove").mockRejectedValue(new Error("Couldn't remove that link."));
      const element = await mount(two());

      await openConfirmation(element);
      button(element, "confirm-remove")!.click();
      await settle(element);

      expect(element.querySelectorAll("[data-feed-id]")).toHaveLength(2);
      expect(element.querySelector('[role="alert"]')?.textContent).toContain("Couldn't remove that link.");
    });
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

    it("keeps each link's icon with it when it moves, so no icon is loaded again", async () => {
      vi.spyOn(MemberFeedsService, "reorder").mockResolvedValue();
      const element = await mount(
        three().map((f) => ({ ...f, faviconUrl: `/api/feed-submission/image-proxy?token=${f.id}` }))
      );
      const iconOf = (id: number) => element.querySelector<HTMLImageElement>(`[data-feed-id="${id}"] img`)!;
      const before = [1, 2, 3].map(iconOf);

      move(rows(element)[2], "up").click();
      await settle(element);
      rows(element)[2].querySelector(".dc-sortable__handle")!.dispatchEvent(drag("dragstart"));
      rows(element)[0].dispatchEvent(drag("dragover", -1));
      rows(element)[0].dispatchEvent(drag("drop", -1));
      await settle(element);

      expect(ids(element)).toEqual([2, 1, 3]);
      expect([1, 2, 3].map(iconOf)).toEqual(before);
      expect([1, 2, 3].map((id) => iconOf(id).getAttribute("src"))).toEqual([
        "/api/feed-submission/image-proxy?token=1",
        "/api/feed-submission/image-proxy?token=2",
        "/api/feed-submission/image-proxy?token=3",
      ]);
    });

    it("hides only an icon that fails to load", async () => {
      const element = await mount(three().map((f) => ({ ...f, faviconUrl: `/proxy?token=${f.id}` })));
      const icons = [...element.querySelectorAll<HTMLImageElement>(".dc-feed-manager__favicon")];

      icons[1].dispatchEvent(new Event("error"));

      expect(icons.map((icon) => icon.hidden)).toEqual([false, true, false]);
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
