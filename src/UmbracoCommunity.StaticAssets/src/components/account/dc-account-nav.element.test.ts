import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { AccountNavElement } from "./dc-account-nav.element";

if (!customElements.get("dc-account-nav")) {
  customElements.define("dc-account-nav", AccountNavElement);
}

// The markup AccountPage.cshtml renders.
const markup = `
  <dc-account-nav>
    <nav class="account-page__nav" aria-label="Account">
      <ul>
        <li><a href="#profile" data-account-nav-link>Profile</a></li>
        <li><a href="#timeline" data-account-nav-link>Timeline</a></li>
        <li><a href="#feeds" data-account-nav-link>Feeds &amp; links</a></li>
      </ul>
    </nav>
    <div>
      <section id="profile" data-account-panel><h2 tabindex="-1">Profile</h2></section>
      <section id="timeline" data-account-panel><h2 tabindex="-1">Timeline</h2></section>
      <section id="feeds" data-account-panel>
        <h2 tabindex="-1">Feeds &amp; links</h2>
        <p id="link-suggestions">Links we found</p>
      </section>
    </div>
  </dc-account-nav>
`;

// The test setup replaces window.location with a plain object; this sets its hash.
function setHash(hash: string) {
  (window as unknown as { location: object }).location = { ...window.location, hash };
}

function mount(hash = ""): AccountNavElement {
  setHash(hash);
  document.body.innerHTML = markup;
  return document.querySelector("dc-account-nav")!;
}

function shown(): string[] {
  return [...document.querySelectorAll<HTMLElement>("[data-account-panel]")].filter((p) => !p.hidden).map((p) => p.id);
}

function link(name: string): HTMLAnchorElement {
  return document.querySelector<HTMLAnchorElement>(`a[href="#${name}"]`)!;
}

function current(): string[] {
  return [...document.querySelectorAll('[aria-current="page"]')].map((a) => a.getAttribute("href")!);
}

describe("dc-account-nav", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
    setHash("");
  });

  it("shows the first panel when the address names none, and marks its link as current", () => {
    mount();

    expect(shown()).toEqual(["profile"]);
    expect(current()).toEqual(["#profile"]);
  });

  it("shows the panel the address's hash names, without moving focus", () => {
    mount("#timeline");

    expect(shown()).toEqual(["timeline"]);
    expect(current()).toEqual(["#timeline"]);
    expect(document.activeElement).toBe(document.body);
  });

  it("shows the panel holding the element the hash names", () => {
    mount("#link-suggestions");

    expect(shown()).toEqual(["feeds"]);
  });

  it("falls back to the first panel for a hash it doesn't know", () => {
    mount("#nothing-here");

    expect(shown()).toEqual(["profile"]);
  });

  it("switches panels from the menu, adds a history entry and moves focus to the panel's heading", () => {
    const pushState = vi.spyOn(history, "pushState").mockImplementation(() => {});
    mount();

    link("feeds").click();

    expect(shown()).toEqual(["feeds"]);
    expect(current()).toEqual(["#feeds"]);
    expect(pushState).toHaveBeenCalledWith(null, "", "#feeds");
    expect(document.activeElement).toBe(document.querySelector("#feeds h2"));
  });

  it("follows back and forward", () => {
    vi.spyOn(history, "pushState").mockImplementation(() => {});
    mount();
    link("timeline").click();
    expect(shown()).toEqual(["timeline"]);

    setHash("#profile");
    window.dispatchEvent(new PopStateEvent("popstate"));

    expect(shown()).toEqual(["profile"]);
    expect(current()).toEqual(["#profile"]);
    expect(document.activeElement).toBe(document.querySelector("#profile h2"));
  });

  it("leaves a click with a modifier key to the browser", () => {
    mount();

    link("feeds").dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true, ctrlKey: true }));

    expect(shown()).toEqual(["profile"]);
  });
});
