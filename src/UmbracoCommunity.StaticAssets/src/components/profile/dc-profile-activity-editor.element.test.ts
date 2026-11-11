import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileActivityEditorElement } from "./dc-profile-activity-editor.element";
import { ProfileActivityEntryService, type ActivityEntry } from "../../services/profile-activity.service";

if (!customElements.get("dc-profile-activity-editor")) {
  customElements.define("dc-profile-activity-editor", ProfileActivityEditorElement);
}

function entry(overrides: Partial<ActivityEntry> = {}): ActivityEntry {
  return {
    id: 1,
    kind: "blog",
    kindLabel: "Blog post",
    title: "My post",
    url: "https://example.dev/my-post",
    date: "2026-10-01",
    description: null,
    state: "Pending",
    rejectionReason: null,
    ...overrides,
  };
}

async function settle(element: ProfileActivityEditorElement) {
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await element.updateComplete;
  }
}

async function mount(entries: ActivityEntry[] = []): Promise<ProfileActivityEditorElement> {
  vi.spyOn(ProfileActivityEntryService, "list").mockResolvedValue(entries);
  const element = new ProfileActivityEditorElement();
  document.body.appendChild(element);
  await settle(element);
  return element;
}

function fill(element: ProfileActivityEditorElement, values: Record<string, string>) {
  for (const [name, value] of Object.entries(values)) {
    (element.querySelector(`[name="${name}"]`) as HTMLInputElement).value = value;
  }
}

describe("dc-profile-activity-editor", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("lists the member's entries with where each one stands", async () => {
    const element = await mount([
      entry({ id: 1, title: "Waiting one" }),
      entry({ id: 2, title: "Shown one", state: "Verified" }),
      entry({ id: 3, title: "Refused one", state: "Rejected", rejectionReason: "That page isn't yours." }),
    ]);

    const text = element.textContent ?? "";
    expect(text).toContain("Waiting to be checked");
    expect(text).toContain("Shown on your profile");
    expect(text).toContain("Not accepted");
    expect(text).toContain("That page isn't yours.");
    // A rejected entry has nothing left to withdraw.
    expect(element.querySelector('[data-entry-id="3"] button')).toBeNull();
    expect(element.querySelector('[data-entry-id="1"] button')).not.toBeNull();
  });

  it("submits the form, shows the new entry as waiting and clears the form", async () => {
    const element = await mount();
    const add = vi.spyOn(ProfileActivityEntryService, "add").mockResolvedValue(entry({ id: 7, title: "Brand new" }));
    fill(element, {
      kind: "videos",
      title: "Brand new",
      url: "https://example.dev/v",
      date: "2026-10-02",
      description: " hello ",
    });

    element.querySelector("form")!.dispatchEvent(new SubmitEvent("submit", { cancelable: true }));
    await settle(element);

    expect(add).toHaveBeenCalledWith({
      kind: "videos",
      title: "Brand new",
      url: "https://example.dev/v",
      date: "2026-10-02",
      description: "hello",
    });
    expect(element.querySelector('[data-entry-id="7"]')).not.toBeNull();
    expect(element.querySelector('[role="status"]')?.textContent).toContain("We'll check it");
    expect((element.querySelector('[name="title"]') as HTMLInputElement).value).toBe("");
  });

  it("shows the server's message and keeps what was typed when adding fails", async () => {
    const element = await mount();
    vi.spyOn(ProfileActivityEntryService, "add").mockRejectedValue(new Error("Enter a link that starts with https://."));
    fill(element, { title: "Oops", url: "http://example.dev", date: "2026-10-02" });

    element.querySelector("form")!.dispatchEvent(new SubmitEvent("submit", { cancelable: true }));
    await settle(element);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("https://");
    expect((element.querySelector('[name="title"]') as HTMLInputElement).value).toBe("Oops");
  });

  it("withdraws an entry and removes it from the list", async () => {
    const element = await mount([entry({ id: 5, title: "Going away", state: "Verified" })]);
    const withdraw = vi.spyOn(ProfileActivityEntryService, "withdraw").mockResolvedValue();

    (element.querySelector('[data-entry-id="5"] button') as HTMLButtonElement).click();
    await settle(element);

    expect(withdraw).toHaveBeenCalledWith(5);
    expect(element.querySelector('[data-entry-id="5"]')).toBeNull();
  });

  it("keeps the entry and says so when withdrawing fails", async () => {
    const element = await mount([entry({ id: 5 })]);
    vi.spyOn(ProfileActivityEntryService, "withdraw").mockRejectedValue(new Error("That entry can't be withdrawn."));

    (element.querySelector('[data-entry-id="5"] button') as HTMLButtonElement).click();
    await settle(element);

    expect(element.querySelector('[data-entry-id="5"]')).not.toBeNull();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("can't be withdrawn");
  });
});
