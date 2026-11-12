import { ServiceBase } from "./service-base";

// Keep in sync with ProfileDisplay.MaxPinnedItems.
export const MAX_PINNED_ITEMS = 3;

// Keep in sync with ProfileDisplay.MaxPinReasonLength.
export const MAX_PIN_REASON_LENGTH = 200;

// Keep in sync with ProfileDisplay.MaxHiddenItems, which also limits the items shown without a preview card.
export const MAX_NO_CARD_ITEMS = 500;

export interface ProfileDisplayItem {
  id: string;
  title: string;
  date: string | null;
}

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
  /** Leave the activity graph off the profile. The graph is on when left out. */
  hideActivityGraph?: boolean;
  /** Show every timeline entry as a plain line. Cards are on when left out. */
  hideTimelineCards?: boolean;
  /** Items shown in the timeline as a plain line, without a preview card. */
  noCardItemIds?: string[];
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
