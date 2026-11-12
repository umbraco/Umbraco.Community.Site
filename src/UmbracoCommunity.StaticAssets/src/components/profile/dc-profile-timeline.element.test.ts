import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { ProfileTimelineElement } from "./dc-profile-timeline.element";

if (!customElements.get("dc-profile-timeline")) {
  customElements.define("dc-profile-timeline", ProfileTimelineElement);
}

// The markup the profile page renders: a plain GET form for the type filter.
const markup = (filters = true) => `
  ${filters
    ? `<form method="get" data-timeline-filters>
        <select name="type" data-timeline-type>
          <option value="">Everything</option>
          <option value="blog">Blog posts</option>
          <option value="videos">Videos</option>
          <option value="packages">Packages</option>
          <option value="other">Other activity</option>
        </select>
        <button type="submit" data-timeline-apply>Show</button>
      </form>`
    : ""}
  <p data-timeline-empty hidden>Nothing here for this filter.</p>
  <section data-timeline-month id="mar-2026">
    <ol>
      <li data-timeline-entry data-type="blog" id="blog-mar">A post</li>
      <li data-timeline-entry data-type="videos" id="video-mar">A video</li>
    </ol>
  </section>
  <section data-timeline-month id="jun-2025">
    <ol>
      <li data-timeline-entry data-type="packages" id="package-jun">A package</li>
    </ol>
  </section>
  <section data-timeline-month id="may-2025">
    <ol>
      <li data-timeline-entry data-type="blog" id="blog-may">An older post</li>
    </ol>
  </section>
`;

// The test setup replaces window.location with a plain object, so the address is kept there by hand.
function setAddress(address: string) {
  const url = new URL(address, "http://localhost:3000");
  (window as { location: unknown }).location = {
    href: url.href,
    search: url.search,
    pathname: url.pathname,
    hostname: url.hostname,
  };
}

function mount(address = "/profiles/octocat", filters = true): ProfileTimelineElement {
  setAddress(address);
  const element = document.createElement("dc-profile-timeline") as ProfileTimelineElement;
  element.innerHTML = markup(filters);
  document.body.appendChild(element);
  return element;
}

function choose(element: ProfileTimelineElement, value: string) {
  const select = element.querySelector<HTMLSelectElement>("[data-timeline-type]")!;
  select.value = value;
  select.dispatchEvent(new Event("change"));
}

const shown = (element: ProfileTimelineElement, selector: string) =>
  [...element.querySelectorAll<HTMLElement>(selector)].filter((el) => !el.hidden).map((el) => el.id);

const empty = (element: ProfileTimelineElement) => element.querySelector<HTMLElement>("[data-timeline-empty]")!;

const search = () => window.location.search;

describe("ProfileTimelineElement", () => {
  beforeEach(() => {
    vi.spyOn(window.history, "replaceState").mockImplementation((_state, _title, url) => setAddress(String(url)));
  });

  afterEach(() => {
    document.body.innerHTML = "";
    setAddress("/");
    vi.restoreAllMocks();
  });

  it("shows every entry to start with and hides the form's own button", () => {
    const element = mount();

    expect(shown(element, "[data-timeline-entry]")).toEqual(["blog-mar", "video-mar", "package-jun", "blog-may"]);
    expect(empty(element).hidden).toBe(true);
    expect(element.querySelector<HTMLElement>("[data-timeline-apply]")!.hidden).toBe(true);
  });

  it("filters by type and hides the months left empty", () => {
    const element = mount();

    choose(element, "blog");

    expect(shown(element, "[data-timeline-entry]")).toEqual(["blog-mar", "blog-may"]);
    expect(shown(element, "[data-timeline-month]")).toEqual(["mar-2026", "may-2025"]);
    expect(empty(element).hidden).toBe(true);
  });

  it("puts the chosen type in the address, keeping the other query values", () => {
    const element = mount("/profiles/octocat?view=everything");

    choose(element, "videos");

    expect(window.location.pathname).toBe("/profiles/octocat");
    expect(new URLSearchParams(search()).get("type")).toBe("videos");
    expect(new URLSearchParams(search()).get("view")).toBe("everything");
  });

  it("takes the type out of the address when everything is chosen again", () => {
    const element = mount("/profiles/octocat?view=everything&type=blog");

    choose(element, "");

    expect(search()).toBe("?view=everything");
    expect(shown(element, "[data-timeline-entry]")).toHaveLength(4);
  });

  it("applies the type from the address on load", () => {
    const element = mount("/profiles/octocat?type=packages");

    expect(element.querySelector<HTMLSelectElement>("[data-timeline-type]")!.value).toBe("packages");
    expect(shown(element, "[data-timeline-entry]")).toEqual(["package-jun"]);
    expect(shown(element, "[data-timeline-month]")).toEqual(["jun-2025"]);
  });

  it("ignores an unknown type in the address and shows everything", () => {
    const element = mount("/profiles/octocat?type=nonsense");

    expect(element.querySelector<HTMLSelectElement>("[data-timeline-type]")!.value).toBe("");
    expect(shown(element, "[data-timeline-entry]")).toHaveLength(4);
    expect(empty(element).hidden).toBe(true);
  });

  it("says so when nothing matches", () => {
    const element = mount();

    choose(element, "other");

    expect(shown(element, "[data-timeline-entry]")).toEqual([]);
    expect(shown(element, "[data-timeline-month]")).toEqual([]);
    expect(empty(element).hidden).toBe(false);
    expect(empty(element).textContent).toBe("Nothing here for this filter.");
  });

  it("filters in place when the form is submitted", () => {
    const element = mount();
    element.querySelector<HTMLSelectElement>("[data-timeline-type]")!.value = "videos";

    const form = element.querySelector<HTMLFormElement>("form")!;
    const submitted = form.dispatchEvent(new Event("submit", { cancelable: true }));

    expect(submitted).toBe(false);
    expect(shown(element, "[data-timeline-entry]")).toEqual(["video-mar"]);
    expect(new URLSearchParams(search()).get("type")).toBe("videos");
  });

  it("lists everything when there is no filter", () => {
    const element = mount("/profiles/octocat?type=blog", false);

    expect(shown(element, "[data-timeline-entry]")).toHaveLength(4);
    expect(empty(element).hidden).toBe(true);
  });
});
