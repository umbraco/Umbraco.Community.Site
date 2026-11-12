import type { DailyCount, RangeQuery, SiteItem, SummaryResponse, TermListResponse, TermQuery } from "./types.js";

const BASE = "/umbraco/searchtracker/api/v1";

interface ApiAuthConfig {
  token?: () => Promise<string | undefined>;
  baseUrl?: string;
  credentials?: RequestCredentials;
}

let _authConfig: ApiAuthConfig = {};

export function setAuthConfig(config: ApiAuthConfig) {
  _authConfig = config;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await _authConfig.token?.();
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const response = await fetch(`${_authConfig.baseUrl ?? ""}${BASE}${path}`, {
    ...init,
    credentials: _authConfig.credentials ?? "same-origin",
    headers,
  });
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}: ${await response.text()}`);
  }
  return response.json() as Promise<T>;
}

function rangeParams(query: RangeQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.siteKey) params.set("siteKey", query.siteKey);
  if (query.from) params.set("from", query.from);
  if (query.to) params.set("to", query.to);
  return params;
}

export const SearchTrackerApi = {
  getSites(): Promise<SiteItem[]> {
    return request<SiteItem[]>("/sites");
  },

  getSummary(query: RangeQuery): Promise<SummaryResponse> {
    return request<SummaryResponse>(`/summary?${rangeParams(query)}`);
  },

  getTerms(query: TermQuery): Promise<TermListResponse> {
    const params = rangeParams(query);
    if (query.contains) params.set("contains", query.contains);
    if (query.nothingFoundOnly) params.set("nothingFoundOnly", "true");
    if (query.sort !== undefined) params.set("sort", String(query.sort));
    if (query.skip !== undefined) params.set("skip", String(query.skip));
    if (query.take !== undefined) params.set("take", String(query.take));
    return request<TermListResponse>(`/terms?${params}`);
  },

  getTermTrend(term: string, query: RangeQuery): Promise<DailyCount[]> {
    const params = rangeParams(query);
    params.set("term", term);
    return request<DailyCount[]>(`/terms/trend?${params}`);
  },

  deleteTerm(term: string, siteKey?: string): Promise<{ deleted: number }> {
    const params = new URLSearchParams({ term });
    if (siteKey) params.set("siteKey", siteKey);
    return request<{ deleted: number }>(`/terms?${params}`, { method: "DELETE" });
  },
};
