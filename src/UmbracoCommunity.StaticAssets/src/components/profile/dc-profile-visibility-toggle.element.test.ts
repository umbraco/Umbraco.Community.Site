import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileVisibilityToggleElement } from "./dc-profile-visibility-toggle.element";
import { ProfileOnboardingService } from "../../services/profile-onboarding.service";

if (!customElements.get("dc-profile-visibility-toggle")) {
  customElements.define("dc-profile-visibility-toggle", ProfileVisibilityToggleElement);
}

async function mount(profileVisible = false): Promise<ProfileVisibilityToggleElement> {
  const element = new ProfileVisibilityToggleElement();
  element.profileVisible = profileVisible;
  document.body.appendChild(element);
  await element.updateComplete;
  return element;
}

function getSwitch(element: ProfileVisibilityToggleElement): HTMLInputElement {
  return element.querySelector<HTMLInputElement>('input[role="switch"]')!;
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
    expect(getHint(element)).toContain("Only you can see it");
    expect(element.querySelector('[role="status"]')?.getAttribute("aria-live")).toBe("polite");
  });

  it("reflects the stored state on first render", async () => {
    const element = await mount(true);
    expect(getSwitch(element).checked).toBe(true);
    expect(getHint(element)).toBe("Your profile is public. Anyone in the world can see it.");
  });

  it("shows the profile when switched on", async () => {
    const spy = vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockResolvedValue();
    const element = await mount(false);

    await toggle(element);

    expect(spy).toHaveBeenCalledWith(true);
    expect(getSwitch(element).checked).toBe(true);
    expect(getHint(element)).toBe("Your profile is public. Anyone in the world can see it.");
    expect(element.querySelector('[role="status"]')?.textContent).toContain("now visible");
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it("hides the profile when switched off", async () => {
    const spy = vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockResolvedValue();
    const element = await mount(true);

    await toggle(element);

    expect(spy).toHaveBeenCalledWith(false);
    expect(getSwitch(element).checked).toBe(false);
    expect(getHint(element)).toContain("Only you can see it");
    expect(element.querySelector('[role="status"]')?.textContent).toContain("now hidden");
  });

  it("moves the switch back and shows the error when saving fails", async () => {
    vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockRejectedValue(new Error("Something went wrong"));
    const element = await mount(false);

    await toggle(element);

    expect(getSwitch(element).checked).toBe(false);
    expect(getHint(element)).toContain("Only you can see it");
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Something went wrong");
    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe("");
  });
});
