import { ServiceBase } from "./service-base";

// Keep in sync with AvatarUploadService.MaxBytes — shared so the onboarding wizard and the
// profile page's avatar editor both fail fast client-side with the same limit.
export const MAX_AVATAR_BYTES = 5_000_000;
export const MAX_AVATAR_MB = MAX_AVATAR_BYTES / 1_000_000;

// Keep in sync with ProfileApiController.MaxBioLength.
export const MAX_BIO_LENGTH = 280;

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
}

export class ProfileOnboardingService extends ServiceBase {
  private static readonly BASE_URL = "/api/profile";

  /** Idempotently starts (or resumes) onboarding for the current member. */
  static async start(): Promise<OnboardingState> {
    const response = await ServiceBase.post(`${this.BASE_URL}/onboarding/start`, {});
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  static async updateBio(bio: string): Promise<void> {
    const response = await ServiceBase.put(`${this.BASE_URL}/bio`, { bio });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
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
    const response = await fetch(`${this.BASE_URL}/avatar`, {
      method: "put",
      body: formData,
    });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const result: { avatarUrl: string; hasCustomAvatar: boolean } = await response.json();
    notifyAvatarChanged(result.avatarUrl);
    return result;
  }

  static async removeAvatar(): Promise<{ avatarUrl: string; hasCustomAvatar: boolean }> {
    const response = await fetch(`${this.BASE_URL}/avatar`, { method: "delete" });
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
