export interface HitListItem {
  id: number;
  hostname: string;
  path: string;
  hitCount: number;
  firstSeenUtc: string;
  lastSeenUtc: string;
  status: number;       // 0 Active, 1 IgnoredManually, 2 Redirected
}

export interface HitListResponse {
  total: number;
  items: HitListItem[];
}

export interface HitQueryStringItem {
  queryString: string;
  hitCount: number;
  lastSeenUtc: string;
}

export interface HitDetail extends HitListItem {
  lastUserAgent: string | null;
  queryStrings: HitQueryStringItem[];
}

export interface IgnoreRuleItem {
  id: number;
  hostname: string | null;
  matchType: number;      // 0 Exact, 1 PathPrefix
  path: string;
  source: number;         // 0 UserDefined, 1 AutoPreset, 2 ConfigSeeded
  note: string | null;
  createdUtc: string;
  isReadOnly: boolean;
}

export interface BulkOpResponse {
  processed: number;
  skipped: number;
}

export interface HitListQuery {
  hostname?: string;
  status?: number;
  search?: string;
  sort?: number;
  skip?: number;
  take?: number;
}
