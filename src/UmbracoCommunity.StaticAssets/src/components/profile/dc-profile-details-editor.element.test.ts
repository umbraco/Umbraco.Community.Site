import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileDetailsEditorElement } from "./dc-profile-details-editor.element";
import {
  ProfileOnboardingService,
  EMPTY_NAME_MESSAGE,
  MAX_AVATAR_BYTES,
  type ProfileDetails,
} from "../../services/profile-onboarding.service";

if (!customElements.get("dc-profile-details-editor")) {
  customElements.define("dc-profile-details-editor", ProfileDetailsEditorElement);
}

interface MountOptions {
  avatarUrl?: string;
  hasCustomAvatar?: boolean;
  displayName?: string;
  headline?: string;
  bio?: string;
  profileUrl?: string;
}

async function mount(options: MountOptions = {}): Promise<ProfileDetailsEditorElement> {
  const element = new ProfileDetailsEditorElement();
  element.avatarUrl = options.avatarUrl ?? "https://github.com/octocat.png";
  element.hasCustomAvatar = options.hasCustomAvatar ?? false;
  element.displayName = options.displayName ?? "The Octocat";
  element.headline = options.headline ?? "";
  element.bio = options.bio ?? "";
  element.profileUrl = options.profileUrl ?? "";
  document.body.appendChild(element);
  await element.updateComplete;
  return element;
}

function details(overrides: Partial<ProfileDetails> = {}): ProfileDetails {
  return {
    displayName: "The Octocat",
    headline: null,
    bio: null,
    ...overrides,
  };
}

async function settle(element: ProfileDetailsEditorElement) {
  // Let the awaited service calls settle, then the re-render.
  await new Promise((resolve) => setTimeout(resolve, 0));
  await element.updateComplete;
}

function field(element: ProfileDetailsEditorElement, label: string): HTMLInputElement | HTMLTextAreaElement {
  const labelElement = Array.from(element.querySelectorAll("label")).find((l) => l.textContent?.trim() === label)!;
  return element.querySelector<HTMLInputElement | HTMLTextAreaElement>(`#${labelElement.htmlFor}`)!;
}

function fileInput(element: ProfileDetailsEditorElement): HTMLInputElement {
  return element.querySelector<HTMLInputElement>('input[type="file"]')!;
}

function buttonByText(element: ProfileDetailsEditorElement, text: string): HTMLButtonElement | undefined {
  return Array.from(element.querySelectorAll("button")).find((button) => button.textContent?.trim() === text);
}

async function type(element: ProfileDetailsEditorElement, input: HTMLInputElement | HTMLTextAreaElement, value: string) {
  input.value = value;
  input.dispatchEvent(new Event("input"));
  await element.updateComplete;
}

async function submit(element: ProfileDetailsEditorElement) {
  element.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
  await settle(element);
}

async function chooseFile(element: ProfileDetailsEditorElement, file: File) {
  const input = fileInput(element);
  Object.defineProperty(input, "files", { value: [file], configurable: true });
  input.dispatchEvent(new Event("change"));
  await settle(element);
}

/** The status beside the Save button. */
function status(element: ProfileDetailsEditorElement): string {
  return element.querySelector(".dc-profile-details-editor__save-status")?.textContent?.trim() ?? "";
}

/** The status beside the photo controls. */
function photoStatus(element: ProfileDetailsEditorElement): string {
  return element.querySelector(".dc-profile-details-editor__status")?.textContent?.trim() ?? "";
}

function profileLink(element: ProfileDetailsEditorElement): HTMLAnchorElement | null {
  return element.querySelector<HTMLAnchorElement>(".dc-profile-details-editor__save-status a");
}

describe("ProfileDetailsEditorElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("renders labelled fields with the member's own text, no placeholders, and a live status", async () => {
    const element = await mount({ headline: "Umbraco developer" });

    const name = field(element, "Name");
    const headline = field(element, "Headline");
    const bio = field(element, "Bio");
    expect(name.value).toBe("The Octocat");
    expect(name.required).toBe(true);
    expect(headline.value).toBe("Umbraco developer");
    expect(headline.required).toBe(false);
    expect(bio.tagName).toBe("TEXTAREA");
    expect(bio.value).toBe("");
    expect(bio.required).toBe(false);
    for (const input of [name, headline, bio]) {
      expect(input.placeholder).toBe("");
    }
    const hint = (input: HTMLInputElement | HTMLTextAreaElement) =>
      element.querySelector(`#${input.id}-hint`)?.textContent?.trim();
    expect(hint(name)).toBe("The name shown on your profile.");
    expect(hint(headline)).toBe("A short line shown under your name. Leave it empty to show no headline.");
    expect(hint(bio)).toBe("A few words about you. Leave it empty to show no bio.");
    expect(element.textContent).not.toContain("GitHub company");
    expect(element.textContent).not.toContain("GitHub bio");
    expect(element.querySelector(".dc-profile-details-editor__save-status")?.getAttribute("role")).toBe("status");
    expect(element.querySelector(".dc-profile-details-editor__save-status")?.getAttribute("aria-live")).toBe("polite");
    expect(element.querySelector(".dc-profile-details-editor__status")?.getAttribute("aria-live")).toBe("polite");
    expect(element.querySelector(`label[for="${fileInput(element).id}"]`)?.textContent).toContain("Upload photo");
  });

  it("saves changed fields", async () => {
    const nameSpy = vi
      .spyOn(ProfileOnboardingService, "updateDisplayName")
      .mockResolvedValue(details({ displayName: "Octo" }));
    const headlineSpy = vi
      .spyOn(ProfileOnboardingService, "updateHeadline")
      .mockResolvedValue(details({ displayName: "Octo", headline: "Umbraco developer" }));
    const bioSpy = vi
      .spyOn(ProfileOnboardingService, "updateBio")
      .mockResolvedValue(details({ displayName: "Octo", headline: "Umbraco developer", bio: "My own words" }));
    const element = await mount();

    await type(element, field(element, "Name"), " Octo ");
    await type(element, field(element, "Headline"), "  Umbraco developer ");
    await type(element, field(element, "Bio"), "My own words");
    await submit(element);

    expect(nameSpy).toHaveBeenCalledWith("Octo");
    expect(headlineSpy).toHaveBeenCalledWith("Umbraco developer");
    expect(bioSpy).toHaveBeenCalledWith("My own words");
    expect(field(element, "Name").value).toBe("Octo");
    expect(field(element, "Headline").value).toBe("Umbraco developer");
    expect(field(element, "Bio").value).toBe("My own words");
    expect(status(element)).toBe("Saved.");
  });

  it("only saves the fields that changed", async () => {
    const nameSpy = vi.spyOn(ProfileOnboardingService, "updateDisplayName");
    const headlineSpy = vi.spyOn(ProfileOnboardingService, "updateHeadline");
    const bioSpy = vi
      .spyOn(ProfileOnboardingService, "updateBio")
      .mockResolvedValue(details({ headline: "Umbraco developer", bio: "Changed" }));
    const element = await mount({ headline: "Umbraco developer", bio: "Original" });

    await type(element, field(element, "Bio"), "Changed");
    await submit(element);

    expect(nameSpy).not.toHaveBeenCalled();
    expect(headlineSpy).not.toHaveBeenCalled();
    expect(bioSpy).toHaveBeenCalledWith("Changed");
  });

  it("clears the headline and bio by saving them empty, with no fallback or reset buttons", async () => {
    const headlineSpy = vi.spyOn(ProfileOnboardingService, "updateHeadline").mockResolvedValue(details({ bio: "My own words" }));
    const bioSpy = vi.spyOn(ProfileOnboardingService, "updateBio").mockResolvedValue(details());
    const element = await mount({ headline: "Umbraco developer", bio: "My own words" });

    await type(element, field(element, "Headline"), "   ");
    await type(element, field(element, "Bio"), "");
    await submit(element);

    expect(headlineSpy).toHaveBeenCalledWith("");
    expect(bioSpy).toHaveBeenCalledWith("");
    expect(field(element, "Headline").value).toBe("");
    expect(field(element, "Bio").value).toBe("");
    expect(field(element, "Bio").placeholder).toBe("");
    expect(status(element)).toBe("Saved.");
    // Only the Save button: no reset, hide or "use GitHub" controls for the text fields.
    expect(Array.from(element.querySelectorAll("button")).map((b) => b.textContent?.trim())).toEqual(["Save"]);
  });

  describe("required name", () => {
    const error = (element: ProfileDetailsEditorElement) =>
      element.querySelector(`#${field(element, "Name").id}-error`);
    const saveButton = (element: ProfileDetailsEditorElement) => buttonByText(element, "Save")!;

    it("shows an inline message and disables Save once the name is emptied", async () => {
      const element = await mount();
      const name = field(element, "Name");
      expect(error(element)).toBeNull();
      expect(name.getAttribute("aria-invalid")).toBe("false");

      await type(element, name, "   ");

      expect(error(element)?.textContent?.trim()).toBe(EMPTY_NAME_MESSAGE);
      expect(error(element)?.getAttribute("role")).toBe("alert");
      expect(name.getAttribute("aria-invalid")).toBe("true");
      expect(name.getAttribute("aria-describedby")).toContain(`${name.id}-error`);
      expect(saveButton(element).disabled).toBe(true);

      await type(element, name, "Octo");

      expect(error(element)).toBeNull();
      expect(name.getAttribute("aria-invalid")).toBe("false");
      expect(saveButton(element).disabled).toBe(false);
    });

    it("never saves an empty name, and focuses the field", async () => {
      const nameSpy = vi.spyOn(ProfileOnboardingService, "updateDisplayName");
      const bioSpy = vi.spyOn(ProfileOnboardingService, "updateBio");
      const element = await mount();
      await type(element, field(element, "Bio"), "New words");
      await type(element, field(element, "Name"), "");

      await submit(element);

      expect(nameSpy).not.toHaveBeenCalled();
      expect(bioSpy).not.toHaveBeenCalled();
      expect(error(element)?.textContent?.trim()).toBe(EMPTY_NAME_MESSAGE);
      expect(document.activeElement).toBe(field(element, "Name"));
      expect(status(element)).toBe("");
    });
  });

  it("keeps the drafts and shows the error when saving fails", async () => {
    vi.spyOn(ProfileOnboardingService, "updateDisplayName").mockRejectedValue(
      new Error("Display name must be 60 characters or fewer.")
    );
    const element = await mount();

    await type(element, field(element, "Name"), "Too long");
    await submit(element);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Display name must be 60 characters or fewer.");
    expect(field(element, "Name").value).toBe("Too long");
    expect(status(element)).toBe("");
  });

  it("updates the photo after an upload", async () => {
    const uploadSpy = vi
      .spyOn(ProfileOnboardingService, "uploadAvatar")
      .mockResolvedValue({ avatarUrl: "/media/me.png", hasCustomAvatar: true });
    const element = await mount();
    const file = new File(["x"], "me.png", { type: "image/png" });

    await chooseFile(element, file);

    expect(uploadSpy).toHaveBeenCalledWith(file);
    expect(element.querySelector("img")?.getAttribute("src")).toBe("/media/me.png");
    expect(buttonByText(element, "Use GitHub photo")).toBeDefined();
    expect(photoStatus(element)).toBe("Your photo has been updated.");
  });

  it("shows the error when an upload fails", async () => {
    vi.spyOn(ProfileOnboardingService, "uploadAvatar").mockRejectedValue(
      new Error("Please upload a GIF, JPEG, PNG, or WEBP image.")
    );
    const element = await mount();

    await chooseFile(element, new File(["x"], "me.txt"));

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Please upload a GIF, JPEG, PNG, or WEBP image.");
    expect(element.querySelector("img")?.getAttribute("src")).toBe("https://github.com/octocat.png");
    expect(buttonByText(element, "Use GitHub photo")).toBeUndefined();
  });

  it("rejects an image that is too large without uploading it", async () => {
    const uploadSpy = vi.spyOn(ProfileOnboardingService, "uploadAvatar");
    const element = await mount();
    const file = new File(["x"], "big.png", { type: "image/png" });
    Object.defineProperty(file, "size", { value: MAX_AVATAR_BYTES + 1 });

    await chooseFile(element, file);

    expect(uploadSpy).not.toHaveBeenCalled();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("too large");
  });

  it("goes back to the GitHub photo", async () => {
    const removeSpy = vi
      .spyOn(ProfileOnboardingService, "removeAvatar")
      .mockResolvedValue({ avatarUrl: "https://github.com/octocat.png", hasCustomAvatar: false });
    const element = await mount({ avatarUrl: "/media/me.png", hasCustomAvatar: true });

    buttonByText(element, "Use GitHub photo")!.click();
    await settle(element);

    expect(removeSpy).toHaveBeenCalledOnce();
    expect(element.querySelector("img")?.getAttribute("src")).toBe("https://github.com/octocat.png");
    expect(buttonByText(element, "Use GitHub photo")).toBeUndefined();
    expect(photoStatus(element)).toBe("Your GitHub photo is used again.");
    expect(document.activeElement).toBe(fileInput(element));
  });

  describe("profile link", () => {
    const profileUrl = "https://example.com/community/profiles/octocat";

    function mockNameSave() {
      return vi
        .spyOn(ProfileOnboardingService, "updateDisplayName")
        .mockResolvedValue(details({ displayName: "Octo" }));
    }

    it("is absent before saving", async () => {
      const element = await mount({ profileUrl });

      expect(profileLink(element)).toBeNull();
    });

    it("appears beside Save in the live status after a successful save", async () => {
      mockNameSave();
      const element = await mount({ profileUrl });

      await type(element, field(element, "Name"), "Octo");
      await submit(element);

      const link = profileLink(element)!;
      expect(link.textContent?.trim()).toBe("View your profile");
      expect(link.getAttribute("href")).toBe(profileUrl);
      expect(link.target).toBe("_blank");
      expect(link.rel).toBe("noopener noreferrer");
      expect(link.closest('[role="status"]')?.getAttribute("aria-live")).toBe("polite");
      expect(link.closest(".dc-profile-details-editor__actions")?.querySelector('button[type="submit"]')).not.toBeNull();
      expect(status(element).replace(/\s+/g, " ")).toBe("Saved. View your profile");
    });

    it("goes away when the form is edited again", async () => {
      mockNameSave();
      const element = await mount({ profileUrl });
      await type(element, field(element, "Name"), "Octo");
      await submit(element);
      expect(profileLink(element)).not.toBeNull();

      await type(element, field(element, "Headline"), "Umbraco developer");

      expect(profileLink(element)).toBeNull();
    });

    it("is absent after a failed save", async () => {
      vi.spyOn(ProfileOnboardingService, "updateDisplayName").mockRejectedValue(new Error("Something went wrong"));
      const element = await mount({ profileUrl });

      await type(element, field(element, "Name"), "Octo");
      await submit(element);

      expect(profileLink(element)).toBeNull();
      expect(element.querySelector('[role="alert"]')?.textContent).toContain("Something went wrong");
    });

    it("is absent when there is nothing to save", async () => {
      const element = await mount({ profileUrl });

      await submit(element);

      expect(profileLink(element)).toBeNull();
      expect(status(element)).toBe("There are no changes to save.");
    });

    it("is absent when there is no profile URL", async () => {
      mockNameSave();
      const element = await mount();

      await type(element, field(element, "Name"), "Octo");
      await submit(element);

      expect(status(element)).toBe("Saved.");
      expect(profileLink(element)).toBeNull();
    });
  });
});
