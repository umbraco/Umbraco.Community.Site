import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileDisplayEditorElement, TIMELINE_PAGE_SIZE } from "./dc-profile-display-editor.element";
import {
  MAX_PIN_REASON_LENGTH,
  ProfileDisplayService,
  type ProfileDisplay,
  type ProfileDisplayChoices,
  type ProfileDisplayItem,
} from "../../services/profile-display.service";

if (!customElements.get("dc-profile-display-editor")) {
  customElements.define("dc-profile-display-editor", ProfileDisplayEditorElement);
}

function display(choices: Partial<ProfileDisplayChoices> = {}): ProfileDisplay {
  return {
    sections: [
      {
        key: "blog",
        label: "Blog posts",
        items: [
          {
            id: "b1",
            title: "First post",
            date: "2026-11-01T12:00:00Z",
            iconKey: "blog",
            url: "https://example.dev/b1",
            meta: "My blog",
            monthKey: "2026-11",
            monthLabel: "Nov 2026",
            order: 0,
            card: { imageUrl: "/media/timeline/b1.png", title: "First post", description: "What it's about" },
          },
          { id: "b2", title: "Second post", date: null, order: 3 },
        ],
      },
      {
        key: "videos",
        label: "Videos",
        items: [
          { id: "v1", title: "A video", date: "2026-10-03T12:00:00Z", monthKey: "2026-10", monthLabel: "Oct 2026", order: 1 },
        ],
      },
      {
        key: "packages",
        label: "Packages",
        items: [
          {
            id: "p1",
            title: "A package",
            date: "2026-10-02T12:00:00Z",
            monthKey: "2026-10",
            monthLabel: "Oct 2026",
            order: 2,
            isRoutineRelease: true,
            meta: "v2.0.0",
          },
        ],
      },
      { key: "forum", label: "Forum highlights", items: [] },
    ],
    choices: { hiddenSections: [], hiddenItemIds: [], pins: [], ...choices },
  };
}

// The save mock answers with what it was sent, as the server does for valid choices.
function mockSave() {
  return vi.spyOn(ProfileDisplayService, "save").mockImplementation(async (choices) => choices);
}

async function settle(element: ProfileDisplayEditorElement) {
  // Let the awaited service calls settle, then the re-render.
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await element.updateComplete;
  }
}

async function mount(initial: ProfileDisplay = display()): Promise<ProfileDisplayEditorElement> {
  vi.spyOn(ProfileDisplayService, "load").mockResolvedValue(initial);
  const element = new ProfileDisplayEditorElement();
  document.body.appendChild(element);
  await settle(element);
  return element;
}

function item(element: ProfileDisplayEditorElement, id: string): HTMLElement {
  return element.querySelector<HTMLElement>(`[data-item-id="${id}"]`)!;
}

function itemIds(element: ProfileDisplayEditorElement): string[] {
  return [...element.querySelectorAll<HTMLElement>("[data-item-id]")].map((el) => el.dataset.itemId!);
}

function pinRows(element: ProfileDisplayEditorElement): HTMLElement[] {
  return [...element.querySelectorAll<HTMLElement>("[data-pin-id]")];
}

function pinIds(element: ProfileDisplayEditorElement): string[] {
  return pinRows(element).map((row) => row.dataset.pinId!);
}

function button(scope: HTMLElement, action: string): HTMLButtonElement {
  return scope.querySelector<HTMLButtonElement>(`[data-action="${action}"]`)!;
}

function sectionSwitch(element: ProfileDisplayEditorElement, label: string): HTMLInputElement {
  const labelElement = [...element.querySelectorAll("label")].find((l) => l.textContent?.includes(label))!;
  return element.querySelector<HTMLInputElement>(`#${labelElement.getAttribute("for")}`)!;
}

function typeFilter(element: ProfileDisplayEditorElement): HTMLSelectElement {
  return element.querySelector<HTMLSelectElement>("[data-timeline-type]")!;
}

async function chooseType(element: ProfileDisplayEditorElement, value: string) {
  const select = typeFilter(element);
  select.value = value;
  select.dispatchEvent(new Event("change"));
  await settle(element);
}

function lastSaved(spy: ReturnType<typeof mockSave>): ProfileDisplayChoices {
  return spy.mock.calls.at(-1)![0];
}

function status(element: ProfileDisplayEditorElement): string {
  return element.querySelector('[role="status"]')?.textContent?.trim() ?? "";
}

/** A drag event as the browser sends it; jsdom has no DragEvent. */
function drag(type: string, clientY = 0): Event {
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperty(event, "clientY", { value: clientY });
  return event;
}

describe("ProfileDisplayEditorElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("offers a labelled switch per section, apart from the items, and a live status region", async () => {
    const element = await mount();

    const blogSwitch = sectionSwitch(element, "Blog posts");
    expect(blogSwitch.getAttribute("role")).toBe("switch");
    expect(blogSwitch.checked).toBe(true);
    expect(blogSwitch.closest("fieldset")?.querySelector("legend")?.textContent).toContain("Sections");
    expect(blogSwitch.closest("[data-item-id]")).toBeNull();
    expect(element.querySelector('[role="status"]')?.getAttribute("aria-live")).toBe("polite");
    expect(element.textContent).toContain("Nothing pinned yet");
  });

  it("lists the items as one timeline, newest first and grouped by month, as the profile does", async () => {
    const element = await mount();

    // The highlights leave out the routine package release, as on the profile.
    expect(itemIds(element)).toEqual(["b1", "v1", "b2"]);
    const months = [...element.querySelectorAll(".community-profile__timeline-month-title")].map((h) => h.textContent);
    expect(months).toEqual(["Nov 2026", "Oct 2026", "No date"]);

    const first = item(element, "b1");
    expect(first.classList.contains("community-profile__timeline-entry")).toBe(true);
    expect(first.querySelector(".community-profile__timeline-icon svg")).not.toBeNull();
    expect(first.querySelector(".community-profile__timeline-icon")?.getAttribute("title")).toBe("Blog posts");
    expect(first.querySelector(".community-profile__timeline-meta")?.textContent).toContain("1 Nov 2026 · My blog");
    expect(first.querySelector("time")?.getAttribute("datetime")).toBe("2026-11-01");
  });

  it("draws an item with a picture as a card, and one without as the plain row", async () => {
    const element = await mount();

    const card = item(element, "b1").querySelector<HTMLAnchorElement>("a.community-profile__timeline-card")!;
    expect(card.classList.contains("community-profile__timeline-card--plain")).toBe(false);
    expect(card.getAttribute("href")).toBe("https://example.dev/b1");
    expect(card.querySelector<HTMLImageElement>(".community-profile__timeline-card-picture")?.getAttribute("src")).toBe(
      "/media/timeline/b1.png"
    );
    expect(card.textContent).toContain("What it's about");

    const plain = item(element, "v1").querySelector(".community-profile__timeline-card")!;
    expect(plain.classList.contains("community-profile__timeline-card--plain")).toBe(true);
    expect(plain.tagName).toBe("DIV");
  });

  it("filters by type, with routine releases under their own type", async () => {
    const element = await mount();

    const options = [...typeFilter(element).options].map((o) => o.textContent);
    expect(options).toEqual(["Highlights", "Blog posts", "Videos", "Packages"]);
    expect(element.textContent).toContain("Highlights leave out routine package releases");

    await chooseType(element, "packages");
    expect(itemIds(element)).toEqual(["p1"]);
    expect(item(element, "p1").textContent).toContain("v2.0.0");
  });

  it("shows a long history a page at a time", async () => {
    const many: ProfileDisplayItem[] = Array.from({ length: TIMELINE_PAGE_SIZE + 5 }, (_, i) => ({
      id: `b${i}`,
      title: `Post ${i}`,
      date: "2026-11-01T12:00:00Z",
      monthKey: "2026-11",
      monthLabel: "Nov 2026",
      order: i,
    }));
    const element = await mount({ sections: [{ key: "blog", label: "Blog posts", items: many }], choices: display().choices });

    expect(itemIds(element)).toHaveLength(TIMELINE_PAGE_SIZE);
    expect(element.textContent).toContain(`Showing ${TIMELINE_PAGE_SIZE} of ${TIMELINE_PAGE_SIZE + 5}`);

    button(element, "more").click();
    await settle(element);

    expect(itemIds(element)).toHaveLength(TIMELINE_PAGE_SIZE + 5);
    expect(button(element, "more")).toBeNull();
    expect(document.activeElement).toBe(button(item(element, `b${TIMELINE_PAGE_SIZE}`), "hide"));
  });

  it("switches a section off and on again", async () => {
    const save = mockSave();
    const element = await mount();

    sectionSwitch(element, "Blog posts").click();
    await settle(element);

    expect(lastSaved(save).hiddenSections).toEqual(["blog"]);
    expect(item(element, "b1")).toBeNull();
    expect(element.textContent).toContain("Not listed, as the section is switched off: Blog posts.");

    sectionSwitch(element, "Blog posts").click();
    await settle(element);

    expect(lastSaved(save).hiddenSections).toEqual([]);
    expect(item(element, "b1")).not.toBeNull();
  });

  it("hides an item, which stays listed dimmed with a Hidden badge, and shows it again", async () => {
    const save = mockSave();
    const element = await mount();

    button(item(element, "b1"), "hide").click();
    await settle(element);

    expect(lastSaved(save).hiddenItemIds).toEqual(["b1"]);
    expect(item(element, "b1").classList.contains("dc-profile-display-editor__entry--hidden")).toBe(true);
    expect(item(element, "b1").querySelector(".dc-profile-display-editor__badge--hidden")?.textContent).toBe("Hidden");
    expect(button(item(element, "b1"), "hide").textContent?.trim()).toBe("Show");
    expect(status(element)).toContain("hidden from your profile");

    button(item(element, "b1"), "hide").click();
    await settle(element);

    expect(lastSaved(save).hiddenItemIds).toEqual([]);
    expect(button(item(element, "b1"), "hide").textContent?.trim()).toBe("Hide");
  });

  it("pins an item, which then shows a Pinned badge and can be unpinned from the timeline", async () => {
    const save = mockSave();
    const element = await mount();

    button(item(element, "v1"), "pin").click();
    await settle(element);

    expect(lastSaved(save).pins).toEqual([{ id: "v1", reason: null }]);
    expect(pinIds(element)).toEqual(["v1"]);
    expect(pinRows(element)[0].textContent).toContain("A video");
    expect(button(item(element, "v1"), "pin")).toBeNull();
    expect(item(element, "v1").querySelector(".dc-profile-display-editor__badge--pinned")?.textContent).toBe("Pinned");
    expect(status(element)).toContain("is pinned");

    button(item(element, "v1"), "unpin").click();
    await settle(element);

    expect(lastSaved(save).pins).toEqual([]);
    expect(button(item(element, "v1"), "pin")).not.toBeNull();
  });

  it("allows no more than three pins", async () => {
    mockSave();
    const element = await mount(
      display({ pins: [{ id: "b1", reason: null }, { id: "b2", reason: null }, { id: "v1", reason: null }] })
    );
    await chooseType(element, "packages");

    expect(button(item(element, "p1"), "pin").disabled).toBe(true);
    expect(element.textContent).toContain("3 of 3 pinned");
  });

  it("doesn't offer to pin a hidden item, and unpins an item when it is hidden", async () => {
    const save = mockSave();
    const element = await mount(display({ pins: [{ id: "b1", reason: "Proud" }], hiddenItemIds: ["b2"] }));

    expect(button(item(element, "b2"), "pin").disabled).toBe(true);

    button(item(element, "b1"), "hide").click();
    await settle(element);

    expect(lastSaved(save)).toMatchObject({ hiddenItemIds: ["b2", "b1"], pins: [] });
    expect(pinIds(element)).toEqual([]);
  });

  it("gives each pin a grab handle and up and down buttons", async () => {
    const element = await mount(display({ pins: [{ id: "b1", reason: null }, { id: "v1", reason: null }] }));

    const row = pinRows(element)[0];
    const handle = row.querySelector<HTMLElement>(".dc-sortable__handle")!;
    expect(handle.getAttribute("draggable")).toBe("true");
    expect(handle.getAttribute("title")).toBe("Drag to reorder");
    expect(handle.querySelector("svg")).not.toBeNull();
    expect(button(row, "up").getAttribute("aria-label")).toBe("Move “First post” up");
    expect(button(row, "up").querySelector("svg")).not.toBeNull();
    expect(button(row, "up").textContent?.trim()).toBe("");
  });

  it("reorders pins with the up and down buttons and keeps focus on the moved item", async () => {
    const save = mockSave();
    const element = await mount(
      display({ pins: [{ id: "b1", reason: "One" }, { id: "v1", reason: null }, { id: "p1", reason: null }] })
    );

    expect(button(pinRows(element)[0], "up").disabled).toBe(true);
    expect(button(pinRows(element)[2], "down").disabled).toBe(true);

    button(pinRows(element)[2], "up").click();
    await settle(element);

    expect(lastSaved(save).pins.map((p) => p.id)).toEqual(["b1", "p1", "v1"]);
    expect(pinIds(element)).toEqual(["b1", "p1", "v1"]);
    expect(document.activeElement).toBe(button(pinRows(element)[1], "up"));
    expect(status(element)).toContain("moved to position 2 of 3");

    button(pinRows(element)[0], "down").click();
    await settle(element);

    expect(lastSaved(save).pins).toEqual([
      { id: "p1", reason: null },
      { id: "b1", reason: "One" },
      { id: "v1", reason: null },
    ]);
  });

  it("moves focus to the other arrow when an item reaches the top", async () => {
    mockSave();
    const element = await mount(display({ pins: [{ id: "b1", reason: null }, { id: "v1", reason: null }] }));

    button(pinRows(element)[1], "up").click();
    await settle(element);

    expect(pinIds(element)).toEqual(["v1", "b1"]);
    expect(document.activeElement).toBe(button(pinRows(element)[0], "down"));
  });

  it("reorders pins by drag and drop, showing where the item will land", async () => {
    const save = mockSave();
    const element = await mount(
      display({ pins: [{ id: "b1", reason: null }, { id: "v1", reason: null }, { id: "p1", reason: null }] })
    );

    pinRows(element)[0].querySelector(".dc-sortable__handle")!.dispatchEvent(drag("dragstart"));
    await settle(element);
    expect(pinRows(element)[0].classList.contains("is-dragging")).toBe(true);

    // Dragged down over the last row: it takes that row's place.
    pinRows(element)[2].dispatchEvent(drag("dragover", 1));
    await settle(element);

    pinRows(element)[2].dispatchEvent(drag("drop", 1));
    await settle(element);

    expect(lastSaved(save).pins.map((p) => p.id)).toEqual(["v1", "p1", "b1"]);
    expect(pinIds(element)).toEqual(["v1", "p1", "b1"]);
    expect(element.querySelector(".is-dragging")).toBeNull();
    expect(status(element)).toContain("moved to position 3 of 3");
  });

  it("unpins an item", async () => {
    const save = mockSave();
    const element = await mount(display({ pins: [{ id: "b1", reason: null }, { id: "v1", reason: null }] }));

    pinRows(element)[0].querySelector<HTMLButtonElement>('button[aria-label^="Unpin"]')!.click();
    await settle(element);

    expect(lastSaved(save).pins.map((p) => p.id)).toEqual(["v1"]);
    expect(pinIds(element)).toEqual(["v1"]);
  });

  it("lists a pinned item that is no longer in the recent activity by its kept title, and lets it be reordered, noted and unpinned", async () => {
    const save = mockSave();
    const element = await mount(
      display({ pins: [{ id: "b1", reason: null }, { id: "old", reason: "Still proud", title: "An old post" }] })
    );

    const oldRow = pinRows(element)[1];
    expect(oldRow.textContent).toContain("An old post");
    expect(oldRow.querySelector("input")!.value).toBe("Still proud");
    expect(item(element, "old")).toBeNull();

    button(oldRow, "up").click();
    await settle(element);
    expect(pinIds(element)).toEqual(["old", "b1"]);
    expect(lastSaved(save).pins.map((p) => p.id)).toEqual(["old", "b1"]);

    const input = pinRows(element)[0].querySelector<HTMLInputElement>("input")!;
    input.value = "Prouder";
    input.dispatchEvent(new Event("input"));
    input.dispatchEvent(new Event("change"));
    await settle(element);
    expect(lastSaved(save).pins.find((p) => p.id === "old")?.reason).toBe("Prouder");

    pinRows(element)[0].querySelector<HTMLButtonElement>('button[aria-label="Unpin “An old post”"]')!.click();
    await settle(element);
    expect(pinIds(element)).toEqual(["b1"]);
  });

  it("saves why the member is proud of a pinned item, trimmed, with a length limit", async () => {
    const save = mockSave();
    const element = await mount(display({ pins: [{ id: "b1", reason: null }] }));
    const input = pinRows(element)[0].querySelector<HTMLInputElement>("input")!;

    expect(element.querySelector(`label[for="${input.id}"]`)?.textContent).toContain("Why you're proud of it");
    expect(input.maxLength).toBe(MAX_PIN_REASON_LENGTH);

    input.value = "  My first package  ";
    input.dispatchEvent(new Event("input"));
    input.dispatchEvent(new Event("change"));
    await settle(element);

    expect(lastSaved(save).pins).toEqual([{ id: "b1", reason: "My first package" }]);
  });

  it("goes back to what was saved and shows the error when saving fails", async () => {
    vi.spyOn(ProfileDisplayService, "save").mockRejectedValue(new Error("You can pin up to 3 items."));
    const element = await mount();

    button(item(element, "b1"), "hide").click();
    await settle(element);

    expect(button(item(element, "b1"), "hide").textContent?.trim()).toBe("Hide");
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("You can pin up to 3 items.");
    expect(status(element)).toBe("");
  });

  it("saves changes made while an earlier save is still running, in order", async () => {
    let release: () => void = () => {};
    const save = vi
      .spyOn(ProfileDisplayService, "save")
      .mockImplementationOnce((choices) => new Promise((resolve) => (release = () => resolve(choices))))
      .mockImplementation(async (choices) => choices);
    const element = await mount();

    button(item(element, "b1"), "hide").click();
    await element.updateComplete;
    button(item(element, "v1"), "pin").click();
    await element.updateComplete;

    expect(save).toHaveBeenCalledTimes(1);
    release();
    await settle(element);

    expect(save).toHaveBeenCalledTimes(2);
    expect(lastSaved(save)).toMatchObject({ hiddenItemIds: ["b1"], pins: [{ id: "v1", reason: null }] });
    expect(item(element, "b1").textContent).toContain("Hidden");
    expect(pinIds(element)).toEqual(["v1"]);
  });

  it("shows the error when the activity can't be loaded", async () => {
    vi.spyOn(ProfileDisplayService, "load").mockRejectedValue(new Error("Finish setting up your profile first."));
    const element = new ProfileDisplayEditorElement();
    document.body.appendChild(element);
    await settle(element);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Finish setting up your profile first.");
  });

  it("offers no timeline switches or plain-line choice, and draws every item with a picture as a card", async () => {
    const element = await mount();

    // Only the section switches are left.
    expect(element.querySelectorAll(".dc-profile-display-editor__switch-list")).toHaveLength(1);
    expect(element.textContent).not.toContain("Show preview cards");
    expect(element.textContent).not.toContain("activity graph");
    expect(element.textContent).not.toContain("Plain line");
    expect(button(item(element, "b1"), "card")).toBeNull();
    expect(item(element, "b1").querySelector(".community-profile__timeline-card-picture")).not.toBeNull();
  });

  it("still lists items from a response without the timeline details", async () => {
    const element = await mount({
      sections: [{ key: "blog", label: "Blog posts", items: [{ id: "x", title: "Plain", date: "2026-01-05T00:00:00Z" }] }],
      choices: display().choices,
    });

    expect(itemIds(element)).toEqual(["x"]);
    expect(item(element, "x").querySelector(".community-profile__timeline-icon svg")).not.toBeNull();
    expect(element.querySelector(".community-profile__timeline-month-title")?.textContent).toBe("Jan 2026");
  });
});
