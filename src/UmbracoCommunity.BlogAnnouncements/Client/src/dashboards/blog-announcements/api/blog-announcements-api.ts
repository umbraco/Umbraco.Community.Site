import type {
  PostListResponse,
  PostDetail,
  RunListResponse,
  SettingsResponse,
  DeliveryResultResponse,
  PollNowResponse,
  PostQuery,
} from "./blog-announcements-types.js";

const BASE = "/umbraco/blogannouncements/api/v1";

// Umbraco's Management API is bearer-authenticated. The dashboard root element consumes
// UMB_AUTH_CONTEXT and calls setAuthConfig() with a token getter so each request attaches a
// fresh Authorization header. Mirrors the NotFoundTracker typed fetch client.
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
    // Cap the raw body: error responses can be full HTML/stack-trace pages, and this message
    // flows straight into notification toasts.
    const text = (await response.text()).slice(0, 200);
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const BlogAnnouncementsApi = {
  listPosts(query: PostQuery): Promise<PostListResponse> {
    const params = new URLSearchParams();
    if (query.status !== undefined) params.set("status", String(query.status));
    if (query.search) params.set("search", query.search);
    if (query.from) params.set("from", query.from);
    if (query.to) params.set("to", query.to);
    if (query.skip !== undefined) params.set("skip", String(query.skip));
    if (query.take !== undefined) params.set("take", String(query.take));
    return request<PostListResponse>(`/posts?${params.toString()}`);
  },

  getPost(id: string): Promise<PostDetail> {
    return request<PostDetail>(`/posts/${id}`);
  },

  announce(id: string, trigger: "Repost" | "PostNow"): Promise<DeliveryResultResponse> {
    return request<DeliveryResultResponse>(`/posts/${id}/announce`, {
      method: "POST",
      body: JSON.stringify({ trigger }),
    });
  },

  /** Marks a post as not announced so the automatic cycle can pick it up again (testing aid). */
  resetPost(id: string): Promise<void> {
    return request<void>(`/posts/${id}/reset`, { method: "POST" });
  },

  listRuns(skip: number, take: number): Promise<RunListResponse> {
    const params = new URLSearchParams({ skip: String(skip), take: String(take) });
    return request<RunListResponse>(`/runs?${params.toString()}`);
  },

  getSettings(): Promise<SettingsResponse> {
    return request<SettingsResponse>(`/settings`);
  },

  /** Triggers one full poll cycle (fetch + detection) on demand. */
  pollNow(): Promise<PollNowResponse> {
    return request<PollNowResponse>(`/poll`, { method: "POST" });
  },

  sendTestMessage(): Promise<DeliveryResultResponse> {
    return request<DeliveryResultResponse>(`/test-message`, { method: "POST" });
  },
};
