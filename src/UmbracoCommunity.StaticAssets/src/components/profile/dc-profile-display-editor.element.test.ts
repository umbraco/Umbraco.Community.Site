import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileDisplayEditorElement } from "./dc-profile-display-editor.element";
import {
  MAX_PIN_REASON_LENGTH,
  ProfileDisplayService,
  type ProfileDisplay,
  type ProfileDisplayChoices,
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
          { id: "b1", title: "First post", date: "2026-11-01T12:00:00Z" },
          { id: "b2", title: "Second post", date: null },
        ],
      },
      { key: "videos", label: "Videos", items: [{ id: "v1", title: "A video", date: null }] },
      { key: "packages", label: "Packages", items: [{ id: "p1", title: "A package", date: null }] },
      { key: "forum", label: "Forum activity", items: [] },
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

function lastSaved(spy: ReturnType<typeof mockSave>): ProfileDisplayChoices {
  return spy.mock.calls.at(-1)![0];
}

function status(element: ProfileDisplayEditorElement): string {
  return element.querySelector('[role="status"]')?.textContent?.trim() ?? "";
}

describe("ProfileDisplayEditorElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("lists each section with a labelled switch, and a live status region", async () => {
    const element = await mount();

    const blogSwitch = sectionSwitch(element, "Show blog posts");
    expect(blogSwitch.getAttribute("role")).toBe("switch");
    expect(blogSwitch.checked).toBe(true);
    expect(item(element, "b1").textContent).toContain("First post");
    expect(item(element, "b1").textContent).toContain("1 Nov 2026");
    expect(element.querySelector('[role="status"]')?.getAttribute("aria-live")).toBe("polite");
    expect(element.textContent).toContain("Nothing pinned yet");
  });

  it("switches a section off and on again", async () => {
    const save = mockSave();
    const element = await mount();

    const blogSwitch = sectionSwitch(element, "Show blog posts");
    blogSwitch.click();
    await settle(element);

    expect(lastSaved(save).hiddenSections).toEqual(["blog"]);
    expect(item(element, "b1")).toBeNull();
    expect(element.textContent).toContain("Not shown on your profile.");

    sectionSwitch(element, "Show blog posts").click();
    await settle(element);

    expect(lastSaved(save).hiddenSections).toEqual([]);
    expect(item(element, "b1")).not.toBeNull();
  });

  it("hides an item and shows it again", async () => {
    const save = mockSave();
    const element = await mount();

    button(item(element, "b1"), "hide").click();
    await settle(element);

    expect(lastSaved(save).hiddenItemIds).toEqual(["b1"]);
    expect(item(element, "b1").textContent).toContain("Hidden");
    expect(button(item(element, "b1"), "hide").textContent?.trim()).toBe("Show");
    expect(status(element)).toContain("hidden from your profile");

    button(item(element, "b1"), "hide").click();
    await settle(element);

    expect(lastSaved(save).hiddenItemIds).toEqual([]);
    expect(button(item(element, "b1"), "hide").textContent?.trim()).toBe("Hide");
  });

  it("pins an item, which then can't be pinned again", async () => {
    const save = mockSave();
    const element = await mount();

    button(item(element, "v1"), "pin").click();
    await settle(element);

    expect(lastSaved(save).pins).toEqual([{ id: "v1", reason: null }]);
    expect(pinIds(element)).toEqual(["v1"]);
    expect(pinRows(element)[0].textContent).toContain("A video");
    expect(button(item(element, "v1"), "pin")).toBeNull();
    expect(status(element)).toContain("is pinned");
  });

  it("allows no more than three pins", async () => {
    mockSave();
    const element = await mount(
      display({ pins: [{ id: "b1", reason: null }, { id: "b2", reason: null }, { id: "v1", reason: null }] })
    );

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
      .mockImplementationOnce(
        (choices) => new Promise((resolve) => (release = () => resolve(choices)))
      )
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

  it("offers the activity graph and preview cards switched on by default", async () => {
    const element = await mount();

    expect(sectionSwitch(element, "Show an activity graph").checked).toBe(true);
    expect(sectionSwitch(element, "Show preview cards").checked).toBe(true);
    expect(button(item(element, "b1"), "card").getAttribute("aria-pressed")).toBe("false");
  });

  it("switches the activity graph off and on again", async () => {
    const save = mockSave();
    const element = await mount();

    sectionSwitch(element, "Show an activity graph").click();
    await settle(element);

    expect(lastSaved(save).hideActivityGraph).toBe(true);
    expect(sectionSwitch(element, "Show an activity graph").checked).toBe(false);
    expect(status(element)).toContain("No longer showing the activity graph");

    sectionSwitch(element, "Show an activity graph").click();
    await settle(element);

    expect(lastSaved(save).hideActivityGraph).toBe(false);
    expect(status(element)).toContain("Showing the activity graph");
  });

  it("switches preview cards off for the whole timeline, which removes the per-item choice", async () => {
    const save = mockSave();
    const element = await mount();

    sectionSwitch(element, "Show preview cards").click();
    await settle(element);

    expect(lastSaved(save).hideTimelineCards).toBe(true);
    expect(button(item(element, "b1"), "card")).toBeNull();
    expect(status(element)).toContain("plain lines only");
  });

  it("shows a single item as a plain line and as a card again", async () => {
    const save = mockSave();
    const element = await mount();

    button(item(element, "b1"), "card").click();
    await settle(element);

    expect(lastSaved(save).noCardItemIds).toEqual(["b1"]);
    expect(item(element, "b1").textContent).toContain("No card");
    expect(button(item(element, "b1"), "card").getAttribute("aria-pressed")).toBe("true");

    button(item(element, "b1"), "card").click();
    await settle(element);

    expect(lastSaved(save).noCardItemIds).toEqual([]);
    expect(item(element, "b1").textContent).not.toContain("No card");
  });

  it("keeps the stored card choices it loaded", async () => {
    const element = await mount(display({ hideActivityGraph: true, hideTimelineCards: false, noCardItemIds: ["v1"] }));

    expect(sectionSwitch(element, "Show an activity graph").checked).toBe(false);
    expect(button(item(element, "v1"), "card").getAttribute("aria-pressed")).toBe("true");
    expect(button(item(element, "b1"), "card").getAttribute("aria-pressed")).toBe("false");
  });

  it("offers no card choice for a hidden item", async () => {
    const element = await mount(display({ hiddenItemIds: ["b1"] }));

    expect(button(item(element, "b1"), "card")).toBeNull();
  });
});
