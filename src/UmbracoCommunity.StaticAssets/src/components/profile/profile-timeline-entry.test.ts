import { describe, it, expect, afterEach } from "vitest";
import { html, render } from "lit";
import { formatTimelineDate, formatTimelineMonth, renderProfileTimelineEntry } from "./profile-timeline-entry";
import { isProfileKindIconKey, profileKindIcon } from "../../svg/profile-kind-icons";

function draw(...args: Parameters<typeof renderProfileTimelineEntry>): HTMLLIElement {
  const host = document.createElement("ol");
  document.body.appendChild(host);
  render(renderProfileTimelineEntry(...args), host);
  return host.querySelector("li")!;
}

describe("renderProfileTimelineEntry", () => {
  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("draws a card as the profile page does", () => {
    const li = draw({
      title: "First post",
      iconKey: "blog",
      typeLabel: "Blog posts",
      url: "https://example.dev/b1",
      date: "2026-11-01T12:00:00Z",
      meta: "My blog",
      card: { imageUrl: "/media/b1.png", title: "First post", description: "About it" },
    });

    expect(li.className).toContain("community-profile__timeline-entry--card");
    expect(li.querySelector(".community-profile__timeline-icon")?.getAttribute("title")).toBe("Blog posts");
    expect(li.querySelector(".sr-only")?.textContent).toBe("Blog posts:");
    const card = li.querySelector<HTMLAnchorElement>("a.community-profile__timeline-card")!;
    expect(card.target).toBe("_blank");
    expect(card.rel).toBe("noopener");
    expect(card.querySelectorAll("img")).toHaveLength(2);
    expect(card.querySelector(".community-profile__timeline-desc")?.textContent).toBe("About it");
    expect(li.querySelector(".community-profile__timeline-meta")?.textContent).toBe("1 Nov 2026 · My blog");
  });

  it("draws the plain row, as a link or not, with the kind icon as its placeholder", () => {
    const linked = draw({ title: "A video", iconKey: "video", typeLabel: "Videos", url: "https://example.dev/v1" });
    expect(linked.querySelector("a.community-profile__timeline-card--plain")).not.toBeNull();
    expect(linked.querySelector(".community-profile__timeline-card-placeholder svg")).not.toBeNull();
    expect(linked.querySelector(".community-profile__timeline-meta")).toBeNull();

    const unlinked = draw({ title: "A meet-up", iconKey: "meetup", typeLabel: "Events", url: null });
    expect(unlinked.querySelector("div.community-profile__timeline-card--plain")).not.toBeNull();
  });

  it("adds the extra content, classes and item id it is given", () => {
    const li = draw(
      { title: "A post", iconKey: "blog", typeLabel: "Blog posts" },
      { itemId: "b1", classes: { "is-special": true }, extra: html`<button>Act</button>` }
    );

    expect(li.dataset.itemId).toBe("b1");
    expect(li.classList.contains("is-special")).toBe(true);
    expect(li.querySelector(".community-profile__timeline-body button")?.textContent).toBe("Act");
  });
});

describe("timeline dates", () => {
  it("formats dates and months in UTC with English month names", () => {
    expect(formatTimelineDate("2026-09-30T23:30:00Z")).toBe("30 Sep 2026");
    expect(formatTimelineMonth("2026-09-30T23:30:00Z")).toBe("Sep 2026");
    expect(formatTimelineDate(null)).toBe("");
    expect(formatTimelineDate("not a date")).toBe("");
  });
});

describe("profileKindIcon", () => {
  it("knows every key the Razor partial does, and nothing else", () => {
    for (const key of ["package", "talk", "blog", "video", "forum", "reply", "solved", "other", "social", "meetup", "linkedin", "mastodon", "bluesky"]) {
      expect(isProfileKindIconKey(key)).toBe(true);
    }
    expect(isProfileKindIconKey("unknown")).toBe(false);
    expect(isProfileKindIconKey("toString")).toBe(false);

    const host = document.createElement("div");
    render(profileKindIcon("solved"), host);
    const svg = host.querySelector("svg")!;
    expect(svg.getAttribute("class")).toBe("community-profile__icon");
    expect(svg.getAttribute("aria-hidden")).toBe("true");
  });
});
