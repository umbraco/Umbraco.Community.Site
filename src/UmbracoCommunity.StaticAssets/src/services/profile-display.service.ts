import { ServiceBase } from "./service-base";

// Keep in sync with ProfileDisplay.MaxPinnedItems.
export const MAX_PINNED_ITEMS = 3;

// Keep in sync with ProfileDisplay.MaxPinReasonLength.
export const MAX_PIN_REASON_LENGTH = 200;

/** A preview card for an item: our stored copy of its page's picture, its title and description. */
export interface ProfileDisplayCard {
  imageUrl: string;
  title: string;
  description?: string | null;
}

/**
 * An item in one of the member's activity sections. Besides its id, title and date it carries what the profile's timeline
 * draws for it (built server side from the same timeline entry the profile page renders); those fields are optional
 * so an older response still works.
 */
export interface ProfileDisplayItem {
  id: string;
  title: string;
  date: string | null;
  /** The activity section it comes from. */
  group?: string;
  /** The kind icon (see svg/profile-kind-icons.ts). */
  iconKey?: string;
  url?: string | null;
  /** The short line of context after the date. */
  meta?: string | null;
  description?: string | null;
  /** A routine package release, which the timeline's highlights leave out. */
  isRoutineRelease?: boolean;
  /** Its preview card when one is ready; null for a plain line. */
  card?: ProfileDisplayCard | null;
  /** The month it is listed under, as yyyy-MM; null when it has no date. */
  monthKey?: string | null;
  /** The month heading, e.g. "Mar 2026". */
  monthLabel?: string | null;
  /** Its place on the whole timeline across every section, from 0 (the newest). */
  order?: number;
}

// Mirrors ProfileTimeline.IconKey, for an item that comes without its icon.
const sectionIcons: Record<string, string> = {
  blog: "blog",
  videos: "video",
  packages: "package",
  meetups: "meetup",
  forum: "forum",
  social: "social",
};

/** The icon for an item: its own, otherwise its section's. */
export const itemIconKey = (item: ProfileDisplayItem, sectionKey: string): string =>
  item.iconKey ?? sectionIcons[item.group ?? sectionKey] ?? "other";

/** One activity section of the member's profile, with every item in it (hidden ones too). */
export interface ProfileDisplaySection {
  key: string;
  label: string;
  items: ProfileDisplayItem[];
}

export interface ProfilePin {
  id: string;
  reason: string | null;
  /** The title kept with the pin, so it can be shown once the item is no longer in the member's recent activity. */
  title?: string | null;
}

/** What the member has chosen their profile's activity sections show. Pins are in display order. */
export interface ProfileDisplayChoices {
  hiddenSections: string[];
  hiddenItemIds: string[];
  pins: ProfilePin[];
}

export interface ProfileDisplay {
  sections: ProfileDisplaySection[];
  choices: ProfileDisplayChoices;
}

export class ProfileDisplayService extends ServiceBase {
  private static readonly BASE_URL = "/api/profile/display";

  /** The member's activity sections and their stored choices. */
  static async load(): Promise<ProfileDisplay> {
    const response = await ServiceBase.get(this.BASE_URL);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Replaces the member's choices and returns them as stored. */
  static async save(choices: ProfileDisplayChoices): Promise<ProfileDisplayChoices> {
    // The server keeps each pinned item's title itself; only the id and reason are sent.
    const response = await ServiceBase.put(this.BASE_URL, {
      ...choices,
      pins: choices.pins.map(({ id, reason }) => ({ id, reason })),
    });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
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
