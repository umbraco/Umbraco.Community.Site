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
}

export interface ActivityEntryList {
  items: ActivityEntry[];
  total: number;
}
