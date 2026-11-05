export interface SiteItem {
  key: string;
  name: string;
}

export interface DailyCount {
  day: string;
  searches: number;
  zeroResultSearches: number;
}

export interface SummaryResponse {
  from: string;
  to: string;
  totalSearches: number;
  zeroResultSearches: number;
  uniqueTerms: number;
  days: DailyCount[];
}

export interface TermStat {
  term: string;
  searches: number;
  zeroResultSearches: number;
  lastSearched: string;
}

export interface TermListResponse {
  total: number;
  items: TermStat[];
}

export enum TermSort {
  Searches = 0,
  ZeroResults = 1,
  LastSearched = 2,
  Term = 3,
}

export interface RangeQuery {
  siteKey?: string;
  from?: string;
  to?: string;
}

export interface TermQuery extends RangeQuery {
  contains?: string;
  zeroResultsOnly?: boolean;
  sort?: TermSort;
  skip?: number;
  take?: number;
}
