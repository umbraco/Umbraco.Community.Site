import { ServiceBase } from "./service-base";

export type MemberFeedSource = "Member" | "Platform";

export interface MemberFeed {
  id: number;
  platform: string;
  url: string;
  isHidden: boolean;
  source: MemberFeedSource;
}

/**
 * Platform-synced feeds were matched to this member automatically by the external content
 * platform; member feeds were added by the member themselves. The single "Platform" comparison
 * lives here so callers never compare against the raw string themselves.
 */
export function isPlatformSourced(feed: Pick<MemberFeed, "source">): boolean {
  return feed.source === "Platform";
}

export class MemberFeedsService extends ServiceBase {
  private static readonly BASE_URL = "/api/member-feeds";

  static async list(): Promise<MemberFeed[]> {
    const response = await ServiceBase.get(this.BASE_URL);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  static async add(platform: string, url: string): Promise<MemberFeed> {
    const response = await ServiceBase.post(this.BASE_URL, { platform, url });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  static async setHidden(id: number, isHidden: boolean): Promise<void> {
    const response = await ServiceBase.put(`${this.BASE_URL}/${id}/${isHidden ? "hide" : "unhide"}`, {});
    if (!response.ok) throw new Error(await this.#errorMessage(response));
  }

  static async remove(id: number, reason?: string): Promise<void> {
    const response = await fetch(`${this.BASE_URL}/${id}`, {
      method: "delete",
      body: JSON.stringify({ reason }),
      headers: { "Content-Type": "application/json" },
    });
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
