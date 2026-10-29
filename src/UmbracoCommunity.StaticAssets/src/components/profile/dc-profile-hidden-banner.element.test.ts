import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileHiddenBannerElement } from "./dc-profile-hidden-banner.element";
import { ProfileOnboardingService } from "../../services/profile-onboarding.service";

if (!customElements.get("dc-profile-hidden-banner")) {
  customElements.define("dc-profile-hidden-banner", ProfileHiddenBannerElement);
}

async function mount(accountUrl = ""): Promise<ProfileHiddenBannerElement> {
  const element = new ProfileHiddenBannerElement();
  element.accountUrl = accountUrl;
  document.body.appendChild(element);
  await element.updateComplete;
  return element;
}

function getButton(element: ProfileHiddenBannerElement): HTMLButtonElement {
  return element.querySelector<HTMLButtonElement>(".dc-profile-hidden-banner__action")!;
}

async function flush(element: ProfileHiddenBannerElement) {
  await new Promise((resolve) => setTimeout(resolve, 0));
  await element.updateComplete;
}

describe("ProfileHiddenBannerElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("explains that only the owner can see the profile", async () => {
    const element = await mount();
    expect(element.textContent).toContain("This profile is currently hidden. Only you can see it.");
    expect(element.textContent).toContain("anyone in the world can see it, not just you");
    expect(getButton(element).textContent?.trim()).toBe("Show my profile");
    expect(element.querySelector(".dc-profile-hidden-banner__note a")).toBeNull();
  });

  it("links to the account page when its URL is given", async () => {
    const element = await mount("/account/");
    expect(element.querySelector(".dc-profile-hidden-banner__note a")?.getAttribute("href")).toBe("/account/");
  });

  it("shows the profile and reloads the page", async () => {
    let resolveSave: () => void = () => {};
    const spy = vi
      .spyOn(ProfileOnboardingService, "setProfileVisible")
      .mockImplementation(() => new Promise<void>((resolve) => (resolveSave = resolve)));
    const reload = vi.spyOn(ProfileHiddenBannerElement.prototype, "reloadPage").mockImplementation(() => {});
    const element = await mount();

    getButton(element).click();
    await element.updateComplete;

    expect(spy).toHaveBeenCalledWith(true);
    expect(getButton(element).disabled).toBe(true);

    resolveSave();
    await flush(element);

    expect(reload).toHaveBeenCalledOnce();
  });

  it("shows the error and enables the button again when saving fails", async () => {
    vi.spyOn(ProfileOnboardingService, "setProfileVisible").mockRejectedValue(new Error("Something went wrong"));
    const reload = vi.spyOn(ProfileHiddenBannerElement.prototype, "reloadPage").mockImplementation(() => {});
    const element = await mount();

    getButton(element).click();
    await flush(element);

    expect(reload).not.toHaveBeenCalled();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Something went wrong");
    expect(getButton(element).disabled).toBe(false);
  });
});
