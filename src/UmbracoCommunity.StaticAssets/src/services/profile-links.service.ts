import { ServiceBase } from "./service-base";

export type LinkSuggestionKind = "Account" | "Feed";

/** An account or feed that looks like the member's and isn't among their links yet. */
export interface LinkSuggestion {
  url: string;
  /** The platform an account is on; null for a feed or when not known. */
  platform: string | null;
  /** The account's username or the feed's name, when known. */
  name: string | null;
  kind: LinkSuggestionKind;
}

export class ProfileLinksService {
  private static readonly BASE_URL = "/api/profile/links";

  /** Accounts and feeds that look like the member's. */
  static async suggestions(): Promise<LinkSuggestion[]> {
    const response = await ServiceBase.get(`${this.BASE_URL}/suggestions`);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Tells us a source isn't the member's; activity from it stops showing on their profile. */
  static async notMe(url: string): Promise<void> {
    const response = await ServiceBase.post(`${this.BASE_URL}/not-me`, { url });
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
