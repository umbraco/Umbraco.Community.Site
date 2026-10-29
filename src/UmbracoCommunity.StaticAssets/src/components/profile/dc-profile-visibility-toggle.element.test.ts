import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileVisibilityToggleElement } from "./dc-profile-visibility-toggle.element";
import { ProfileOnboardingService } from "../../services/profile-onboarding.service";

if (!customElements.get("dc-profile-visibility-toggle")) {
  customElements.define("dc-profile-visibility-toggle", ProfileVisibilityToggleElement);
}

async function mount(profileVisible = false, profileUrl = ""): Promise<ProfileVisibilityToggleElement> {
  const element = new ProfileVisibilityToggleElement();
  element.profileVisible = profileVisible;
  element.profileUrl = profileUrl;
  document.body.appendChild(element);
  await element.updateComplete;
  return element;
}

function getSwitch(element: ProfileVisibilityToggleElement): HTMLInputElement {
  return element.querySelector<HTMLInputElement>('input[role="switch"]')!;
}

const profileUrl = "https://example.com/community/profiles/octocat";

function getUrlLabel(element: ProfileVisibilityToggleElement): string {
  return element.querySelector(".dc-profile-visibility-toggle__url-label")?.textContent?.trim() ?? "";
}

function getUrlLink(element: ProfileVisibilityToggleElement): HTMLAnchorElement | null {
  return element.querySelector<HTMLAnchorElement>(`a[href="${profileUrl}"]`);
}

function getHint(element: ProfileVisibilityToggleElement): string {
  const id = getSwitch(element).getAttribute("aria-describedby");
  return element.querySelector(`#${id}`)?.textContent?.trim() ?? "";
}

async function toggle(element: ProfileVisibilityToggleElement) {
  getSwitch(element).click();
  // Let the awaited service call settle, then the re-render.
  await new Promise((resolve) => setTimeout(resolve, 0));
  await element.updateComplete;
}

describe("ProfileVisibilityToggleElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("renders an accessible switch with a label, hint and live status", async () => {
    const element = await mount();
    const input = getSwitch(element);

    expect(input.type).toBe("checkbox");
    expect(input.checked).toBe(false);
    expect(element.querySelector(`label[for="${input.id}"]`)?.textContent).toContain("Show my profile");
    expect(getHint(element)).toContain("the rest of the community can't find you");
    expect(element.querySelector('[role="status"]')?.getAttribute("aria-live")).toBe("polite");
  });

  it("reflects the stored state on first render", async () => {
    const element = await mount(true);
    expect(getSwitch(element).checked).toBe(true);
    expect(getHint(element)).toBe("Your profile is public. This is where the rest of the community finds you.");
  });

  it("shows the profile when switched on", async () => {
    const spy = vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockResolvedValue();
    const element = await mount(false);

    await toggle(element);

    expect(spy).toHaveBeenCalledWith(true);
    expect(getSwitch(element).checked).toBe(true);
    expect(getHint(element)).toBe("Your profile is public. This is where the rest of the community finds you.");
    expect(element.querySelector('[role="status"]')?.textContent).toContain("now visible");
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it("hides the profile when switched off", async () => {
    const spy = vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockResolvedValue();
    const element = await mount(true);

    await toggle(element);

    expect(spy).toHaveBeenCalledWith(false);
    expect(getSwitch(element).checked).toBe(false);
    expect(getHint(element)).toContain("the rest of the community can't find you");
    expect(element.querySelector('[role="status"]')?.textContent).toContain("now hidden");
  });

  it("moves the switch back and shows the error when saving fails", async () => {
    vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockRejectedValue(new Error("Something went wrong"));
    const element = await mount(false);

    await toggle(element);

    expect(getSwitch(element).checked).toBe(false);
    expect(getHint(element)).toContain("the rest of the community can't find you");
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Something went wrong");
    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe("");
  });

  it("warns that a hidden profile can't be found, and that showing it makes it public", async () => {
    const element = await mount(false);

    expect(getHint(element)).toContain("Your profile is hidden.");
    expect(getHint(element)).toContain("the rest of the community can't find you until you turn on Show my profile");
    expect(getHint(element)).toContain("anyone in the world can see it");
  });

  it("shows the profile link with a label that matches the stored state", async () => {
    const hidden = await mount(false, profileUrl);
    expect(getUrlLabel(hidden)).toBe("Only you can see this link for now:");
    const link = getUrlLink(hidden)!;
    expect(link.textContent).toBe(profileUrl);
    expect(link.target).toBe("_blank");
    expect(link.rel).toBe("noopener noreferrer");

    document.body.innerHTML = "";
    const visible = await mount(true, profileUrl);
    expect(getUrlLabel(visible)).toBe("Your public profile link:");
  });

  it("updates the hint and link label as soon as the switch is flipped", async () => {
    vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockResolvedValue();
    const element = await mount(false, profileUrl);

    await toggle(element);

    expect(getHint(element)).toBe("Your profile is public. This is where the rest of the community finds you.");
    expect(getUrlLabel(element)).toBe("Your public profile link:");

    await toggle(element);

    expect(getHint(element)).toContain("the rest of the community can't find you");
    expect(getUrlLabel(element)).toBe("Only you can see this link for now:");
  });

  it("puts the link label back when saving fails", async () => {
    vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockRejectedValue(new Error("Something went wrong"));
    const element = await mount(false, profileUrl);

    await toggle(element);

    expect(getUrlLabel(element)).toBe("Only you can see this link for now:");
  });

  it("shows no link when there is no profile URL", async () => {
    const element = await mount(true);

    expect(element.querySelector(".dc-profile-visibility-toggle__url-label")).toBeNull();
    expect(element.querySelector("a")).toBeNull();
  });
});
