import { ServiceBase } from "./service-base";

export type OnboardingStatus = "NotStarted" | "InProgress" | "Completed";

export interface OnboardingState {
  handle: string;
  displayName: string;
  bio: string | null;
  avatarUrl: string;
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

  static async uploadAvatar(file: File): Promise<{ avatarUrl: string }> {
    const formData = new FormData();
    formData.append("file", file);
    // Not ServiceBase.put — that always JSON-encodes the body. A multipart file upload
    // needs FormData with no explicit Content-Type (the browser sets the boundary).
    const response = await fetch(`${this.BASE_URL}/avatar`, {
      method: "put",
      body: formData,
    });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
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
