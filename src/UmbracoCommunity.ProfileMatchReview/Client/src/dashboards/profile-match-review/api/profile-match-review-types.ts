export type MatchState = "Held" | "Confirmed" | "Rejected";

export interface MatchSignal {
  name: string;
  detail: string;
}

export interface ProfileMatch {
  id: number;
  handle: string;
  displayName: string;
  sourceUrl: string;
  state: MatchState;
  confidence: number;
  signals: MatchSignal[];
  decidedBy: "Automatic" | "Manual" | "Member";
  decidedByUserName: string | null;
  decidedUtc: string;
}

export interface ProfileMatchList {
  items: ProfileMatch[];
  total: number;
}

/**
 * Where a hand-added entry stands: "Verified" = on the member's profile (every new entry), "Rejected" = removed by a
 * moderator, "Pending" = legacy, added while entries were still checked first and not decided yet.
 */
export type ActivityEntryState = "Pending" | "Verified" | "Rejected";

export interface ActivityEntry {
  id: number;
  handle: string;
  displayName: string;
  kind: string;
  kindLabel: string;
  title: string;
  url: string;
  /** yyyy-MM-dd */
  date: string;
  description: string | null;
  state: ActivityEntryState;
  rejectionReason: string | null;
  decidedByUserName: string | null;
  decidedUtc: string | null;
  createdUtc: string;
  /** The picture the member attached (served from the site), shown publicly on the timeline while the entry is on the profile; or null. */
  imageUrl: string | null;
}

export interface ActivityEntryList {
  items: ActivityEntry[];
  total: number;
}
