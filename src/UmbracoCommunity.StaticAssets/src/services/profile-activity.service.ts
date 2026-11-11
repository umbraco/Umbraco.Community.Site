import { ServiceBase } from "./service-base";

// Keep in sync with ProfileActivityEntry.MaxTitleLength and ProfileActivityEntry.MaxDescriptionLength.
export const MAX_ACTIVITY_TITLE_LENGTH = 200;
export const MAX_ACTIVITY_DESCRIPTION_LENGTH = 500;

/** The sections a hand-added entry can go in. Keep in sync with ProfileActivityEntry.Kinds. */
export const ACTIVITY_KINDS: ReadonlyArray<{ key: string; label: string }> = [
  { key: "blog", label: "Blog post" },
  { key: "videos", label: "Video" },
  { key: "packages", label: "Package" },
  { key: "event", label: "Event" },
  { key: "talk", label: "Talk" },
  { key: "contribution", label: "Open-source contribution" },
  { key: "podcast", label: "Podcast or interview" },
  { key: "other", label: "Other" },
];

export type ActivityEntryState = "Pending" | "Verified" | "Rejected";

export interface ActivityEntry {
  id: number;
  kind: string;
  kindLabel: string;
  title: string;
  url: string;
  /** yyyy-MM-dd */
  date: string;
  description: string | null;
  state: ActivityEntryState;
  rejectionReason: string | null;
}

export interface NewActivityEntry {
  kind: string;
  title: string;
  url: string;
  date: string;
  description: string;
}

export class ProfileActivityEntryService {
  private static readonly BASE_URL = "/api/profile/activity";

  /** The member's entries that are waiting, shown or rejected, newest first. */
  static async list(): Promise<ActivityEntry[]> {
    const response = await ServiceBase.get(this.BASE_URL);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Adds an entry that waits for a check, and returns it as stored. */
  static async add(entry: NewActivityEntry): Promise<ActivityEntry> {
    const response = await ServiceBase.post(this.BASE_URL, entry);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Withdraws an entry; one that was already shown on the profile stops showing. */
  static async withdraw(id: number): Promise<void> {
    const response = await ServiceBase.delete(`${this.BASE_URL}/${id}`);
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
