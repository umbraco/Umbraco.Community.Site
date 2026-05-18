import type { HitListResponse, HitListQuery, HitDetail, IgnoreRuleItem, BulkOpResponse } from "./types";

const BASE = "/umbraco/umbracocommunitynotfoundtracker/api/v1";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    ...init,
    credentials: "include",
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const NotFoundTrackerApi = {
  listHits(query: HitListQuery): Promise<HitListResponse> {
    const params = new URLSearchParams();
    if (query.hostname) params.set("hostname", query.hostname);
    if (query.status !== undefined) params.set("status", String(query.status));
    if (query.search) params.set("search", query.search);
    if (query.sort !== undefined) params.set("sort", String(query.sort));
    if (query.skip !== undefined) params.set("skip", String(query.skip));
    if (query.take !== undefined) params.set("take", String(query.take));
    return request<HitListResponse>(`/hits?${params.toString()}`);
  },

  getHit(id: number): Promise<HitDetail> {
    return request<HitDetail>(`/hits/${id}`);
  },

  getHostnames(): Promise<string[]> {
    return request<string[]>(`/hits/hostnames`);
  },

  deleteHit(id: number): Promise<void> {
    return request<void>(`/hits/${id}`, { method: "DELETE" });
  },

  bulkDeleteHits(ids: number[]): Promise<BulkOpResponse> {
    return request<BulkOpResponse>(`/hits/bulk-delete`, {
      method: "POST",
      body: JSON.stringify({ ids }),
    });
  },

  createRedirect(hitId: number, targetContentKey: string, culture: string | null): Promise<void> {
    return request<void>(`/hits/${hitId}/redirect`, {
      method: "POST",
      body: JSON.stringify({ targetContentKey, culture }),
    });
  },

  ignoreFromHit(hitId: number, path: string, matchType: number, hostname: string | null, note: string | null): Promise<void> {
    return request<void>(`/hits/${hitId}/ignore`, {
      method: "POST",
      body: JSON.stringify({ path, matchType, hostname, note }),
    });
  },

  bulkIgnoreHits(ids: number[], matchType: number): Promise<BulkOpResponse> {
    return request<BulkOpResponse>(`/hits/bulk-ignore`, {
      method: "POST",
      body: JSON.stringify({ ids, matchType }),
    });
  },

  listIgnoreRules(): Promise<IgnoreRuleItem[]> {
    return request<IgnoreRuleItem[]>(`/ignore-rules`);
  },

  createIgnoreRule(path: string, matchType: number, hostname: string | null, note: string | null): Promise<IgnoreRuleItem> {
    return request<IgnoreRuleItem>(`/ignore-rules`, {
      method: "POST",
      body: JSON.stringify({ path, matchType, hostname, note }),
    });
  },

  updateIgnoreRule(id: number, path: string, matchType: number, hostname: string | null, note: string | null): Promise<IgnoreRuleItem> {
    return request<IgnoreRuleItem>(`/ignore-rules/${id}`, {
      method: "PUT",
      body: JSON.stringify({ path, matchType, hostname, note }),
    });
  },

  deleteIgnoreRule(id: number): Promise<void> {
    return request<void>(`/ignore-rules/${id}`, { method: "DELETE" });
  },

  reseedAutoPreset(): Promise<void> {
    return request<void>(`/ignore-rules/reseed-auto-preset`, { method: "POST" });
  },
};
