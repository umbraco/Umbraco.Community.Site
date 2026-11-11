import type { MatchState, ProfileMatch, ProfileMatchList, ActivityEntry, ActivityEntryList, ActivityEntryState } from "./profile-match-review-types.js";

const BASE = "/umbraco/profilematchreview/api/v1";

// Umbraco's Management API is bearer-authenticated. The dashboard root element consumes UMB_AUTH_CONTEXT and calls
// setAuthConfig() with a token getter so each request attaches a fresh Authorization header.
interface ApiAuthConfig {
  token?: () => Promise<string | undefined>;
  baseUrl?: string;
  credentials?: RequestCredentials;
}

let _authConfig: ApiAuthConfig = {};

export function setAuthConfig(config: ApiAuthConfig) {
  _authConfig = config;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
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
    const text = (await response.text()).slice(0, 200);
    throw new ApiError(response.status, `${response.status} ${response.statusText}: ${text}`);
  }
  return response.json() as Promise<T>;
}

/** True when the signed-in user is in the reviewer group. Any other failure is treated as no access. */
export async function hasReviewAccess(): Promise<boolean> {
  try {
    const token = await _authConfig.token?.();
    const response = await fetch(`${_authConfig.baseUrl ?? ""}${BASE}/access`, {
      credentials: _authConfig.credentials ?? "same-origin",
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    return response.ok;
  } catch {
    return false;
  }
}

export const ProfileMatchReviewApi = {
  list(state: MatchState, skip: number, take: number): Promise<ProfileMatchList> {
    const params = new URLSearchParams({ state, skip: String(skip), take: String(take) });
    return request<ProfileMatchList>(`/matches?${params.toString()}`);
  },

  link(id: number): Promise<ProfileMatch> {
    return request<ProfileMatch>(`/matches/${id}/link`, { method: "POST" });
  },

  reject(id: number): Promise<ProfileMatch> {
    return request<ProfileMatch>(`/matches/${id}/reject`, { method: "POST" });
  },

  listActivity(state: ActivityEntryState, skip: number, take: number): Promise<ActivityEntryList> {
    const params = new URLSearchParams({ state, skip: String(skip), take: String(take) });
    return request<ActivityEntryList>(`/activity?${params.toString()}`);
  },

  verifyActivity(id: number): Promise<ActivityEntry> {
    return request<ActivityEntry>(`/activity/${id}/verify`, { method: "POST" });
  },

  rejectActivity(id: number, reason: string): Promise<ActivityEntry> {
    return request<ActivityEntry>(`/activity/${id}/reject`, { method: "POST", body: JSON.stringify({ reason }) });
  },
};
