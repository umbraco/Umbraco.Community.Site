import { ServiceBase } from "./service-base";
import { antiforgeryFetch } from "./antiforgery";

// Keep in sync with AvatarUploadService.MaxBytes, so the avatar editor fails fast client-side.
export const MAX_AVATAR_BYTES = 5_000_000;
export const MAX_AVATAR_MB = MAX_AVATAR_BYTES / 1_000_000;

// Keep in sync with ProfileText.MaxBioLength.
export const MAX_BIO_LENGTH = 280;

// Keep in sync with ProfileText.MaxHeadlineLength.
export const MAX_HEADLINE_LENGTH = 100;

// Keep in sync with ProfileText.MaxDisplayNameLength.
export const MAX_DISPLAY_NAME_LENGTH = 60;

// Keep in sync with the empty-name message in ProfileApiController.UpdateDisplayName.
export const EMPTY_NAME_MESSAGE = "Please enter your name. It can't be empty.";

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

/**
 * Dispatched on window after the member's display name, headline or bio is saved, with the details as now shown on
 * their profile, so a preview of the profile on the page (the account page's header) can follow.
 */
export const PROFILE_DETAILS_CHANGED_EVENT = "dc-profile-details-changed";

/** Dispatched on window after the member shows or hides their profile. */
export const PROFILE_VISIBILITY_CHANGED_EVENT = "dc-profile-visibility-changed";

export interface ProfileVisibilityChangedDetail {
  visible: boolean;
}

function notifyDetailsChanged(details: ProfileDetails): void {
  window.dispatchEvent(new CustomEvent<ProfileDetails>(PROFILE_DETAILS_CHANGED_EVENT, { detail: details }));
}

/**
 * The member's display name, headline and bio, as returned after any of them is saved: always the member's own text,
 * never a GitHub fallback. A null headline or bio is not shown on the profile.
 */
export interface ProfileDetails {
  displayName: string;
  headline: string | null;
  bio: string | null;
}

export class ProfileOnboardingService extends ServiceBase {
  private static readonly BASE_URL = "/api/profile";

  /** Saves the member's own bio. An empty bio clears it, so no bio is shown. */
  static async updateBio(bio: string): Promise<ProfileDetails> {
    const response = await ServiceBase.put(`${this.BASE_URL}/bio`, { bio: bio.trim() || null });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const details: ProfileDetails = await response.json();
    notifyDetailsChanged(details);
    return details;
  }

  /** Saves the member's own headline. An empty headline clears it, so no headline is shown. */
  static async updateHeadline(headline: string): Promise<ProfileDetails> {
    const response = await ServiceBase.put(`${this.BASE_URL}/headline`, { headline: headline.trim() || null });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const details: ProfileDetails = await response.json();
    notifyDetailsChanged(details);
    return details;
  }

  /**
   * Saves the member's own display name. A name is required: an empty one is refused here, without a request, and
   * by the server.
   */
  static async updateDisplayName(displayName: string): Promise<ProfileDetails> {
    const name = displayName.trim();
    if (!name) throw new Error(EMPTY_NAME_MESSAGE);
    const response = await ServiceBase.put(`${this.BASE_URL}/display-name`, { displayName: name });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const details: ProfileDetails = await response.json();
    notifyDetailsChanged(details);
    return details;
  }

  /** Shows or hides the current member's profile. */
  static async setProfileVisible(visible: boolean): Promise<void> {
    const response = await ServiceBase.put(`${this.BASE_URL}/visibility`, { visible });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    window.dispatchEvent(
      new CustomEvent<ProfileVisibilityChangedDetail>(PROFILE_VISIBILITY_CHANGED_EVENT, { detail: { visible } })
    );
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
