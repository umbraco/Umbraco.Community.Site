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
  decidedBy: "Automatic" | "Manual";
  decidedByUserName: string | null;
  decidedUtc: string;
}

export interface ProfileMatchList {
  items: ProfileMatch[];
  total: number;
}
