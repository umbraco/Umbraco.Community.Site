import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileActivityEditorElement } from "./dc-profile-activity-editor.element";
import {
  ProfileActivityEntryService,
  type ActivityEntry,
  type ActivityLinkDetails,
} from "../../services/profile-activity.service";

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
    state: "Verified",
    rejectionReason: null,
    imageUrl: null,
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

/** Chooses a file in the picture input, as the browser would. */
async function choosePicture(element: ProfileActivityEditorElement, file: File) {
  const input = element.querySelector('input[name="image"]') as HTMLInputElement;
  Object.defineProperty(input, "files", { value: [file], configurable: true });
  input.dispatchEvent(new Event("change"));
  await settle(element);
}

function picture(type = "image/png", size = 10): File {
  return new File([new Uint8Array(size)], "photo.png", { type });
}

function fill(element: ProfileActivityEditorElement, values: Record<string, string>) {
  for (const [name, value] of Object.entries(values)) {
    (element.querySelector(`[name="${name}"]`) as HTMLInputElement).value = value;
  }
}

function field(element: ProfileActivityEditorElement, name: string): HTMLInputElement {
  return element.querySelector(`[name="${name}"]`) as HTMLInputElement;
}

function button(element: ProfileActivityEditorElement, text: string): HTMLButtonElement {
  return Array.from(element.querySelectorAll("button")).find((b) => b.textContent?.trim() === text) as HTMLButtonElement;
}

function submit(element: ProfileActivityEditorElement) {
  element.querySelector("form")!.dispatchEvent(new SubmitEvent("submit", { cancelable: true }));
}

function details(overrides: Partial<ActivityLinkDetails> = {}): ActivityLinkDetails {
  return { kind: null, title: null, date: null, description: null, imageUrl: null, ...overrides };
}

/** Types a link and leaves the field, as the member would. */
async function enterLink(element: ProfileActivityEditorElement, url: string) {
  const input = field(element, "url");
  input.value = url;
  input.dispatchEvent(new Event("input"));
  input.dispatchEvent(new Event("change"));
  await settle(element);
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
      entry({ id: 1, title: "Waiting one", state: "Pending" }),
      entry({ id: 2, title: "Shown one", state: "Verified" }),
      entry({ id: 3, title: "Removed one", state: "Rejected", rejectionReason: "That page isn't yours." }),
    ]);

    const text = element.textContent ?? "";
    expect(text).toContain("Waiting for a moderator");
    expect(text).toContain("On your profile");
    expect(text).toContain("Removed by a moderator");
    expect(text).toContain("That page isn't yours.");
    // A removed entry can't be edited back onto the profile, but it can be withdrawn.
    expect(element.querySelector('[data-item-id="3"] [data-edit-id]')).toBeNull();
    expect(element.querySelector('[data-item-id="3"] [data-withdraw-id="3"]')).not.toBeNull();
    expect(element.querySelector('[data-item-id="1"] [data-edit-id="1"]')).not.toBeNull();
    expect(element.querySelector('[data-edit-id="2"]')?.getAttribute("aria-label")).toBe("Edit “Shown one”");
  });

  it("asks for the link first", async () => {
    const element = await mount();

    const names = Array.from(element.querySelectorAll("form [name]")).map((f) => f.getAttribute("name"));
    expect(names).toEqual(["url", "kind", "title", "date", "description", "image"]);
    expect(button(element, "Add to my profile")).toBeTruthy();
  });

  it("submits the form, shows the new entry on the profile and clears the form", async () => {
    const element = await mount();
    const add = vi.spyOn(ProfileActivityEntryService, "add").mockResolvedValue(entry({ id: 7, title: "Brand new" }));
    fill(element, {
      kind: "videos",
      title: "Brand new",
      url: "https://example.dev/v",
      date: "2026-10-02",
      description: " hello ",
    });

    submit(element);
    await settle(element);

    expect(add).toHaveBeenCalledWith({
      kind: "videos",
      title: "Brand new",
      url: "https://example.dev/v",
      date: "2026-10-02",
      description: "hello",
      image: null,
    });
    expect(element.querySelector('[data-item-id="7"]')?.textContent).toContain("On your profile");
    expect(element.querySelector('.dc-profile-activity-editor__status[role="status"]')?.textContent).toContain(
      "“Brand new” is on your profile now."
    );
    expect(field(element, "title").value).toBe("");
  });

  it("shows the server's message and keeps what was typed when adding fails", async () => {
    const element = await mount();
    vi.spyOn(ProfileActivityEntryService, "add").mockRejectedValue(new Error("Enter a link that starts with https://."));
    fill(element, { title: "Oops", url: "http://example.dev", date: "2026-10-02" });

    submit(element);
    await settle(element);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("https://");
    expect(field(element, "title").value).toBe("Oops");
  });

  it("withdraws an entry and removes it from the list", async () => {
    const element = await mount([entry({ id: 5, title: "Going away", state: "Verified" })]);
    const withdraw = vi.spyOn(ProfileActivityEntryService, "withdraw").mockResolvedValue();

    (element.querySelector('[data-withdraw-id="5"]') as HTMLButtonElement).click();
    await settle(element);

    expect(withdraw).toHaveBeenCalledWith(5);
    expect(element.querySelector('[data-item-id="5"]')).toBeNull();
  });

  it("keeps the entry and says so when withdrawing fails", async () => {
    const element = await mount([entry({ id: 5 })]);
    vi.spyOn(ProfileActivityEntryService, "withdraw").mockRejectedValue(new Error("That entry can't be withdrawn."));

    (element.querySelector('[data-withdraw-id="5"]') as HTMLButtonElement).click();
    await settle(element);

    expect(element.querySelector('[data-item-id="5"]')).not.toBeNull();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("can't be withdrawn");
  });

  describe("filling in from the page", () => {
    beforeEach(() => {
      URL.createObjectURL = vi.fn(() => "blob:preview");
      URL.revokeObjectURL = vi.fn();
    });

    it("looks the link up when it is entered and fills in the empty fields", async () => {
      const element = await mount();
      const lookUp = vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(
        details({ kind: "videos", title: "A talk", date: "2026-09-30", description: "About it." })
      );

      await enterLink(element, "https://example.dev/talk");

      expect(lookUp).toHaveBeenCalledWith("https://example.dev/talk");
      expect(field(element, "kind").value).toBe("videos");
      expect(field(element, "title").value).toBe("A talk");
      expect(field(element, "date").value).toBe("2026-09-30");
      expect(field(element, "description").value).toBe("About it.");
      expect(element.textContent).toContain("Filled in from the page. Check the details and add it.");
    });

    it("never overwrites what the member typed", async () => {
      const element = await mount();
      vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(
        details({ kind: "videos", title: "The page's title", description: "The page's words." })
      );
      fill(element, { title: "My own title", kind: "talk" });
      field(element, "kind").dispatchEvent(new Event("change"));

      await enterLink(element, "https://example.dev/talk");

      expect(field(element, "title").value).toBe("My own title");
      expect(field(element, "kind").value).toBe("talk");
      expect(field(element, "description").value).toBe("The page's words.");
    });

    it("replaces what an earlier lookup filled in when the link changes", async () => {
      const element = await mount();
      vi.spyOn(ProfileActivityEntryService, "lookUp")
        .mockResolvedValueOnce(details({ title: "First page" }))
        .mockResolvedValueOnce(details({ title: "Second page" }));

      await enterLink(element, "https://example.dev/one");
      await enterLink(element, "https://example.dev/two");

      expect(field(element, "title").value).toBe("Second page");
    });

    it("doesn't look up a link that isn't https, or the same link twice", async () => {
      const element = await mount();
      const lookUp = vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(details({ title: "T" }));

      await enterLink(element, "http://example.dev/plain");
      await enterLink(element, "https://example.dev/a");
      field(element, "url").dispatchEvent(new Event("change"));
      await settle(element);

      expect(lookUp).toHaveBeenCalledTimes(1);
    });

    it("stays silent and usable when the page gives nothing", async () => {
      const element = await mount();
      vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(details());

      await enterLink(element, "https://example.dev/nothing");

      expect(field(element, "title").value).toBe("");
      expect(element.querySelector('[role="alert"]')).toBeNull();
      expect(element.textContent).not.toContain("Looking up the page");
      expect(element.textContent).not.toContain("Filled in");
    });

    it("says it is looking the page up while it does, and keeps the form usable", async () => {
      const element = await mount();
      let answer: (value: ActivityLinkDetails) => void = () => {};
      vi.spyOn(ProfileActivityEntryService, "lookUp").mockReturnValue(new Promise((resolve) => (answer = resolve)));

      await enterLink(element, "https://example.dev/slow");

      expect(element.textContent).toContain("Looking up the page…");
      expect(field(element, "title").disabled).toBe(false);
      expect(button(element, "Add to my profile").disabled).toBe(false);
      answer(details({ title: "Slow page" }));
      await settle(element);
      expect(field(element, "title").value).toBe("Slow page");
    });

    it("shows the page's picture as the one that will be used, with the file input to use a different one", async () => {
      const element = await mount();
      vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(
        details({ title: "T", imageUrl: "/media/profile-timeline/abc.png" })
      );

      await enterLink(element, "https://example.dev/pic");

      const img = element.querySelector(".dc-profile-activity-editor__page-picture img") as HTMLImageElement;
      expect(img.getAttribute("src")).toBe("/media/profile-timeline/abc.png");
      expect(element.textContent).toContain("This picture from the page will be used.");
      const input = field(element, "image");
      expect(element.querySelector(`label[for="${input.id}"]`)?.textContent).toBe("Use a different picture (optional)");

      await choosePicture(element, picture());
      expect(element.querySelector(".dc-profile-activity-editor__page-picture")).toBeNull();
    });

    it("the Fetch details button looks the link up", async () => {
      const element = await mount();
      const lookUp = vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(details({ title: "T" }));
      fill(element, { url: "https://example.dev/b" });

      button(element, "Fetch details").click();
      await settle(element);

      expect(lookUp).toHaveBeenCalledWith("https://example.dev/b");
    });
  });

  describe("editing", () => {
    beforeEach(() => {
      URL.createObjectURL = vi.fn(() => "blob:preview");
      URL.revokeObjectURL = vi.fn();
    });

    async function startEditing(element: ProfileActivityEditorElement, id: number) {
      (element.querySelector(`[data-edit-id="${id}"]`) as HTMLButtonElement).click();
      await settle(element);
    }

    it("fills the form with the entry and switches it to editing", async () => {
      const element = await mount([
        entry({ id: 4, kind: "talk", title: "My talk", url: "https://example.dev/t", date: "2026-09-01", description: "Words" }),
      ]);

      await startEditing(element, 4);

      const heading = element.querySelector(".dc-profile-activity-editor__form-heading") as HTMLElement;
      expect(heading.textContent?.trim()).toBe("Edit activity");
      expect(document.activeElement).toBe(heading);
      expect(field(element, "kind").value).toBe("talk");
      expect(field(element, "title").value).toBe("My talk");
      expect(field(element, "url").value).toBe("https://example.dev/t");
      expect(field(element, "date").value).toBe("2026-09-01");
      expect(field(element, "description").value).toBe("Words");
      expect(button(element, "Save changes")).toBeTruthy();
      expect(button(element, "Cancel")).toBeTruthy();
    });

    it("saves the changes and updates the entry in place", async () => {
      const element = await mount([entry({ id: 4, title: "Old title" }), entry({ id: 9, title: "Other" })]);
      const update = vi.spyOn(ProfileActivityEntryService, "update").mockResolvedValue(entry({ id: 4, title: "New title" }));
      await startEditing(element, 4);
      fill(element, { title: "New title" });

      submit(element);
      await settle(element);

      expect(update).toHaveBeenCalledWith(4, expect.objectContaining({ title: "New title", image: null, removeImage: false }));
      const items = Array.from(element.querySelectorAll("[data-item-id]")).map((li) => li.getAttribute("data-item-id"));
      expect(items).toEqual(["4", "9"]);
      expect(element.querySelector('[data-item-id="4"]')?.textContent).toContain("New title");
      expect(element.textContent).toContain("“New title” is updated.");
      expect(button(element, "Add to my profile")).toBeTruthy();
      expect(document.activeElement).toBe(element.querySelector('[data-edit-id="4"]'));
    });

    it("doesn't look the link up on its own while editing", async () => {
      const element = await mount([entry({ id: 4 })]);
      const lookUp = vi.spyOn(ProfileActivityEntryService, "lookUp").mockResolvedValue(details({ title: "Page title" }));
      await startEditing(element, 4);

      await enterLink(element, "https://example.dev/changed");

      expect(lookUp).not.toHaveBeenCalled();
      expect(field(element, "title").value).toBe("My post");
    });

    it("shows the current picture and can remove it", async () => {
      const element = await mount([entry({ id: 4, imageUrl: "/media/profile-activity/a.png" })]);
      const update = vi.spyOn(ProfileActivityEntryService, "update").mockResolvedValue(entry({ id: 4 }));
      await startEditing(element, 4);

      const current = element.querySelector(".dc-profile-activity-editor__picture-preview img") as HTMLImageElement;
      expect(current.getAttribute("src")).toBe("/media/profile-activity/a.png");
      expect(element.querySelector(`label[for="${field(element, "image").id}"]`)?.textContent).toBe("Replace picture (optional)");

      button(element, "Remove picture").click();
      await settle(element);
      expect(element.querySelector(".dc-profile-activity-editor__picture-preview")).toBeNull();

      submit(element);
      await settle(element);
      expect(update.mock.calls[0][1].removeImage).toBe(true);
    });

    it("sends a new picture to replace the current one", async () => {
      const element = await mount([entry({ id: 4, imageUrl: "/media/profile-activity/a.png" })]);
      const update = vi.spyOn(ProfileActivityEntryService, "update").mockResolvedValue(entry({ id: 4 }));
      await startEditing(element, 4);
      const file = picture();

      await choosePicture(element, file);
      submit(element);
      await settle(element);

      expect(update.mock.calls[0][1].image).toBe(file);
    });

    it("cancels without saving, back to adding, and returns focus to the entry", async () => {
      const element = await mount([entry({ id: 4 })]);
      const update = vi.spyOn(ProfileActivityEntryService, "update");
      await startEditing(element, 4);

      button(element, "Cancel").click();
      await settle(element);

      expect(update).not.toHaveBeenCalled();
      expect(button(element, "Add to my profile")).toBeTruthy();
      expect(field(element, "title").value).toBe("");
      expect(document.activeElement).toBe(element.querySelector('[data-edit-id="4"]'));
    });

    it("keeps editing and shows the message when saving fails", async () => {
      const element = await mount([entry({ id: 4 })]);
      vi.spyOn(ProfileActivityEntryService, "update").mockRejectedValue(new Error("You've already added that link."));
      await startEditing(element, 4);

      submit(element);
      await settle(element);

      expect(element.querySelector('[role="alert"]')?.textContent).toContain("already added that link");
      expect(button(element, "Save changes")).toBeTruthy();
    });
  });

  describe("picture", () => {
    beforeEach(() => {
      URL.createObjectURL = vi.fn(() => "blob:preview");
      URL.revokeObjectURL = vi.fn();
    });

    it("has a labelled file input with its hint", async () => {
      const element = await mount();
      const input = element.querySelector('input[name="image"]') as HTMLInputElement;

      expect(input.type).toBe("file");
      expect(input.accept).toBe("image/jpeg,image/png,image/gif,image/webp");
      expect(element.querySelector(`label[for="${input.id}"]`)?.textContent).toBe("Picture (optional)");
      const hint = element.querySelector(`#${input.getAttribute("aria-describedby")}`);
      expect(hint?.textContent).toContain("JPEG, PNG, GIF or WebP, up to 2 MB. Shown on the card in your timeline.");
    });

    it("previews a chosen picture and sends it with the entry, then clears it", async () => {
      const element = await mount();
      const add = vi.spyOn(ProfileActivityEntryService, "add").mockResolvedValue(entry({ id: 8, imageUrl: "/media/profile-activity/a.png" }));
      const file = picture();

      await choosePicture(element, file);
      const preview = element.querySelector(".dc-profile-activity-editor__picture-preview img") as HTMLImageElement;
      expect(preview.getAttribute("src")).toBe("blob:preview");
      expect(preview.alt).not.toBe("");

      fill(element, { title: "With a picture", url: "https://example.dev/p", date: "2026-10-02" });
      submit(element);
      await settle(element);

      expect(add.mock.calls[0][0].image).toBe(file);
      expect(element.querySelector(".dc-profile-activity-editor__picture-preview")).toBeNull();
      expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");
    });

    it("keeps the picture when adding fails", async () => {
      const element = await mount();
      vi.spyOn(ProfileActivityEntryService, "add").mockRejectedValue(new Error("Something went wrong."));

      await choosePicture(element, picture());
      submit(element);
      await settle(element);

      expect(element.querySelector(".dc-profile-activity-editor__picture-preview img")).not.toBeNull();
    });

    it("removes the chosen picture so the entry is sent without one", async () => {
      const element = await mount();
      const add = vi.spyOn(ProfileActivityEntryService, "add").mockResolvedValue(entry());

      await choosePicture(element, picture());
      button(element, "Remove picture").click();
      await settle(element);

      expect(element.querySelector(".dc-profile-activity-editor__picture-preview")).toBeNull();
      expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");

      submit(element);
      await settle(element);
      expect(add.mock.calls[0][0].image).toBeNull();
    });

    it.each([
      ["a type that isn't accepted", picture("image/svg+xml"), "JPEG, PNG, GIF or WebP"],
      ["a picture that is too large", picture("image/png", 2_000_001), "2 MB"],
    ])("refuses %s with a message tied to the input", async (_, file, message) => {
      const element = await mount();

      await choosePicture(element, file);

      const input = element.querySelector('input[name="image"]') as HTMLInputElement;
      const error = element.querySelector('.dc-profile-activity-editor__picture [role="alert"]');
      expect(error?.textContent).toContain(message);
      expect(input.getAttribute("aria-invalid")).toBe("true");
      expect(input.getAttribute("aria-describedby")).toContain(error!.id);
      expect(element.querySelector(".dc-profile-activity-editor__picture-preview")).toBeNull();
    });
  });
});
