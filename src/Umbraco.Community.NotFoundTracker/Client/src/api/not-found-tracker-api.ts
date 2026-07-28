import type { HitListResponse, HitListQuery, HitDetail, IgnoreRuleItem, BulkOpResponse, HostnameGroup } from "./types";

const BASE = "/umbraco/umbracocommunitynotfoundtracker/api/v1";

// Umbraco's Management API is bearer-authenticated. The dashboard root element
// consumes UMB_AUTH_CONTEXT and calls setAuthConfig() with a token getter so
// each request can attach a fresh Authorization header.
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
  const baseUrl = _authConfig.baseUrl ?? "";
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const response = await fetch(`${baseUrl}${BASE}${path}`, {
    ...init,
    credentials: _authConfig.credentials ?? "same-origin",
    headers,
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
    if (query.hostnames?.length) {
      for (const h of query.hostnames) params.append("hostname", h);
    }
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

  getHostnameGroups(): Promise<HostnameGroup[]> {
    return request<HostnameGroup[]>(`/hits/hostname-groups`);
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
