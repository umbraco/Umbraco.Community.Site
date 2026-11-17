import { antiforgeryFetch } from "./antiforgery";
import { ServiceBase } from "./service-base";

// Keep in sync with ProfileActivityEntry.MaxTitleLength and ProfileActivityEntry.MaxDescriptionLength.
export const MAX_ACTIVITY_TITLE_LENGTH = 200;
export const MAX_ACTIVITY_DESCRIPTION_LENGTH = 500;

/** The largest picture an entry can carry. Keep in sync with ProfileActivityEntry.MaxImageBytes. */
export const MAX_ACTIVITY_IMAGE_BYTES = 2_000_000;
export const MAX_ACTIVITY_IMAGE_MB = MAX_ACTIVITY_IMAGE_BYTES / 1_000_000;

/** The picture formats accepted (the server checks the bytes too). Keep in sync with ProfileTimelineImageStore.Sniff. */
export const ACTIVITY_IMAGE_TYPES: ReadonlyArray<string> = ["image/jpeg", "image/png", "image/gif", "image/webp"];

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

/**
 * Where an entry stands: "Verified" = on the profile (every new entry), "Rejected" = removed by a moderator (with a
 * note), "Pending" = added while entries were still checked first and not decided yet.
 */
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
  /** The picture the member attached, served from this site, or null. */
  imageUrl: string | null;
  /** Our copy of the linked page's picture, shown on the card when the entry has no picture of its own. */
  pageImageUrl?: string | null;
}

export interface NewActivityEntry {
  kind: string;
  title: string;
  url: string;
  date: string;
  description: string;
  /** An optional picture for the entry's timeline card. */
  image?: File | null;
}

export interface ActivityEntryChanges extends NewActivityEntry {
  /** Removes the entry's picture when no new `image` is chosen. Without either, the picture is kept. */
  removeImage?: boolean;
}

/** What the page behind a link offers for a new entry; each part is null when the page didn't say. */
export interface ActivityLinkDetails {
  /** A guess, one of ACTIVITY_KINDS' keys. */
  kind: string | null;
  title: string | null;
  /** yyyy-MM-dd */
  date: string | null;
  description: string | null;
  /** Our copy of the page's picture, served from this site; the timeline card uses it when no picture is attached. */
  imageUrl: string | null;
}

/** True for a link the server will look up: an absolute https address. */
export function isLookupLink(value: string): boolean {
  try {
    const url = new URL(value.trim());
    return url.protocol === "https:" && !!url.hostname;
  } catch {
    return false;
  }
}

/**
 * Why a picture can't be attached, or null when it can. The server checks again (and looks at the bytes, not
 * the type the browser reports).
 */
export function activityImageError(file: File): string | null {
  if (!ACTIVITY_IMAGE_TYPES.includes(file.type)) {
    return "Choose a JPEG, PNG, GIF or WebP picture.";
  }
  if (file.size > MAX_ACTIVITY_IMAGE_BYTES) {
    return `That picture is too large. Choose one of up to ${MAX_ACTIVITY_IMAGE_MB} MB.`;
  }
  return null;
}

export class ProfileActivityEntryService {
  private static readonly BASE_URL = "/api/profile/activity";

  /** The member's entries that are shown, removed by a moderator or (from before) waiting, newest first. */
  static async list(): Promise<ActivityEntry[]> {
    const response = await ServiceBase.get(this.BASE_URL);
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Adds an entry, on the profile at once, with its picture if it has one, and returns it as stored. */
  static async add(entry: NewActivityEntry): Promise<ActivityEntry> {
    return this.#send(this.BASE_URL, "post", this.#formData(entry));
  }

  /**
   * Edits one of the member's entries. A new `image` replaces the picture; `removeImage` removes it; otherwise it is
   * kept. Returns the entry as stored.
   */
  static async update(id: number, entry: ActivityEntryChanges): Promise<ActivityEntry> {
    const formData = this.#formData(entry);
    if (!entry.image && entry.removeImage) {
      formData.append("removeImage", "true");
    }
    return this.#send(`${this.BASE_URL}/${id}`, "put", formData);
  }

  /**
   * What the page behind `url` offers, read by the server. Never throws: a page that can't be read, or any error,
   * gives an empty answer, and the member just types.
   */
  static async lookUp(url: string): Promise<ActivityLinkDetails> {
    const empty: ActivityLinkDetails = { kind: null, title: null, date: null, description: null, imageUrl: null };
    try {
      const response = await ServiceBase.post(`${this.BASE_URL}/preview`, { url });
      if (!response.ok) return empty;
      return { ...empty, ...((await response.json()) as Partial<ActivityLinkDetails>) };
    } catch {
      return empty;
    }
  }

  static #formData(entry: NewActivityEntry): FormData {
    const formData = new FormData();
    formData.append("kind", entry.kind);
    formData.append("title", entry.title);
    formData.append("url", entry.url);
    formData.append("date", entry.date);
    formData.append("description", entry.description);
    if (entry.image) {
      formData.append("image", entry.image);
    }
    return formData;
  }

  // Not ServiceBase.post, which always JSON-encodes the body: a multipart upload needs FormData with no explicit
  // Content-Type (the browser sets the boundary). antiforgeryFetch still adds the antiforgery token header.
  static async #send(url: string, method: "post" | "put", body: FormData): Promise<ActivityEntry> {
    const response = await antiforgeryFetch(url, { method, body });
    if (!response.ok) throw new Error(await this.#errorMessage(response));
    return response.json();
  }

  /** Withdraws an entry; one that was shown on the profile stops showing. One a moderator removed can be withdrawn too. */
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
