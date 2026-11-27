import { ServiceBase } from "./service-base";

/** One of the member's Umbraco forum badges, as `GET /api/profile/badges` lists it. */
export interface ProfileBadge {
  id: number;
  name: string;
  description: string | null;
  /** The badge's picture through this site's image proxy, or null to show the generic badge icon. */
  iconUrl: string | null;
  /** "Gold", "Silver" or "Bronze", or null. */
  badgeType: string | null;
  /** How many forum members have the badge, or null. */
  grantCount: number | null;
  grantedAt: string | null;
  /** Whether the member's profile shows it. */
  shown: boolean;
  /** The badge's page on the forum (built by the server from its id and slug), or null. */
  url: string | null;
}

/**
 * - `ok`: the badges are listed (possibly none);
 * - `noForumLink`: the member has no link to their forum profile (under Feeds & links), so there is nothing to list;
 * - `notFound`: the forum has no user by the name in that link;
 * - `unavailable`: the forum could not be reached just now.
 */
export type ProfileBadgesStatus = "ok" | "noForumLink" | "notFound" | "unavailable";

export interface ProfileBadges {
  status: ProfileBadgesStatus;
  /** The forum username from the member's forum link (not necessarily their GitHub handle), or null without one. */
  forumUsername: string | null;
  forumProfileUrl: string | null;
  badges: ProfileBadge[];
  shownIds: number[];
  maxShown: number;
}

/**
 * Dispatched on window after the member saves which badges their profile shows, so the account page's profile header
 * can show the new row. The detail carries the shown badges in profile order.
 */
export const PROFILE_BADGES_CHANGED_EVENT = "dc-profile-badges-changed";

export interface ProfileBadgesChangedDetail {
  shown: ProfileBadge[];
}

/** Tells the page which badges the profile shows now (see PROFILE_BADGES_CHANGED_EVENT). */
export function notifyBadgesChanged(shown: ProfileBadge[]): void {
  window.dispatchEvent(new CustomEvent<ProfileBadgesChangedDetail>(PROFILE_BADGES_CHANGED_EVENT, { detail: { shown } }));
}

/** The badges a profile shows, in the order it shows them. */
export function shownBadges(badges: ProfileBadges): ProfileBadge[] {
  return badges.badges.filter((badge) => badge.shown);
}

/** What kind of badge it is in words (e.g. "Gold forum badge"); matches ProfileHeroViewModel.BadgeKind. */
export function badgeKind(badge: Pick<ProfileBadge, "badgeType">): string {
  return badge.badgeType ? `${badge.badgeType} forum badge` : "Forum badge";
}

export class ProfileBadgesService extends ServiceBase {
  private static readonly BASE_URL = "/api/profile/badges";

  static async load(): Promise<ProfileBadges> {
    const response = await ServiceBase.get(this.BASE_URL);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Replaces the badges the profile shows and tells the page (see PROFILE_BADGES_CHANGED_EVENT). */
  static async setShown(badgeIds: number[]): Promise<ProfileBadges> {
    const response = await ServiceBase.put(this.BASE_URL, { badgeIds });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    const saved: ProfileBadges = await response.json();
    notifyBadgesChanged(shownBadges(saved));
    return saved;
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
