import { ServiceBase } from "./service-base";
import { antiforgeryFetch } from "./antiforgery";

// Keep in sync with AvatarUploadService.MaxBytes — shared so the onboarding wizard and the
// profile page's avatar editor both fail fast client-side with the same limit.
export const MAX_AVATAR_BYTES = 5_000_000;
export const MAX_AVATAR_MB = MAX_AVATAR_BYTES / 1_000_000;

// Keep in sync with ProfileText.MaxBioLength.
export const MAX_BIO_LENGTH = 280;

// Keep in sync with ProfileText.MaxHeadlineLength.
export const MAX_HEADLINE_LENGTH = 100;

// Keep in sync with ProfileText.MaxDisplayNameLength.
export const MAX_DISPLAY_NAME_LENGTH = 60;

/**
 * Dispatched on window after the signed-in member's avatar is uploaded or removed, so other
 * parts of the page (such as the header avatar) can show the new image straight away.
 */
export const PROFILE_AVATAR_CHANGED_EVENT = "dc-profile-avatar-changed";

export interface ProfileAvatarChangedDetail {
  avatarUrl: string;
}

function notifyAvatarChanged(avatarUrl: string): void {
  window.dispatchEvent(
    new CustomEvent<ProfileAvatarChangedDetail>(PROFILE_AVATAR_CHANGED_EVENT, { detail: { avatarUrl } })
  );
}

export type OnboardingStatus = "NotStarted" | "InProgress" | "Completed";

export interface OnboardingState {
  handle: string;
  displayName: string;
  bio: string | null;
  avatarUrl: string;
  hasCustomAvatar: boolean;
  onboardingStatus: OnboardingStatus;
  /** The bio from the member's GitHub profile, used when they haven't written their own. */
  gitHubBio: string | null;
}

/**
 * The member's display name, headline and bio, as returned after any of them is saved. Each
 * shown value is the member's own when its `hasCustom…` flag is true, otherwise the GitHub
 * default next to it.
 */
export interface ProfileDetails {
  displayName: string;
  hasCustomDisplayName: boolean;
  defaultDisplayName: string;
  headline: string | null;
  hasCustomHeadline: boolean;
  gitHubHeadline: string | null;
  bio: string | null;
  hasCustomBio: boolean;
  gitHubBio: string | null;
}

export class ProfileOnboardingService extends ServiceBase {
  private static readonly BASE_URL = "/api/profile";

  /** Idempotently starts (or resumes) onboarding for the current member. */
  static async start(): Promise<OnboardingState> {
    const response = await ServiceBase.post(`${this.BASE_URL}/onboarding/start`, {});
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Saves the member's own bio. An empty bio clears it, so their GitHub bio shows again. */
  static async updateBio(bio: string): Promise<ProfileDetails> {
    const response = await ServiceBase.put(`${this.BASE_URL}/bio`, { bio: bio.trim() || null });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Saves the member's own headline. An empty headline clears it, so their GitHub company shows again. */
  static async updateHeadline(headline: string): Promise<ProfileDetails> {
    const response = await ServiceBase.put(`${this.BASE_URL}/headline`, { headline: headline.trim() || null });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Saves the member's own display name. An empty name clears it, so their GitHub name shows again. */
  static async updateDisplayName(displayName: string): Promise<ProfileDetails> {
    const response = await ServiceBase.put(`${this.BASE_URL}/display-name`, { displayName: displayName.trim() || null });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Shows or hides the current member's profile. */
  static async setProfileVisible(visible: boolean): Promise<void> {
    const response = await ServiceBase.put(`${this.BASE_URL}/visibility`, { visible });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
  }

  static async uploadAvatar(file: File): Promise<{ avatarUrl: string; hasCustomAvatar: boolean }> {
    const formData = new FormData();
    formData.append("file", file);
    // Not ServiceBase.put — that always JSON-encodes the body. A multipart file upload
    // needs FormData with no explicit Content-Type (the browser sets the boundary).
    // antiforgeryFetch still adds the antiforgery token header.
    const response = await antiforgeryFetch(`${this.BASE_URL}/avatar`, {
      method: "put",
      body: formData,
    });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const result: { avatarUrl: string; hasCustomAvatar: boolean } = await response.json();
    notifyAvatarChanged(result.avatarUrl);
    return result;
  }

  static async removeAvatar(): Promise<{ avatarUrl: string; hasCustomAvatar: boolean }> {
    const response = await ServiceBase.delete(`${this.BASE_URL}/avatar`);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const result: { avatarUrl: string; hasCustomAvatar: boolean } = await response.json();
    notifyAvatarChanged(result.avatarUrl);
    return result;
  }

  static async complete(): Promise<void> {
    const response = await ServiceBase.post(`${this.BASE_URL}/onboarding/complete`, {});
    if (!response.ok) throw new Error(await this.#errorMessage(response));
  }

  static async #errorMessage(response: Response): Promise<string> {
    try {
      const body = await response.json();
      if (body && typeof body.error === "string" && body.error) {
        return body.error;
      }
    } catch {
      // Fall through to statusText when the body isn't the expected JSON shape.
    }
    return response.statusText;
  }
}
