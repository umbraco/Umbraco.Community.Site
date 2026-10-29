import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  ANTIFORGERY_TOKEN_URL,
  antiforgeryFetch,
  requiresAntiforgeryToken,
  resetAntiforgeryToken,
} from "./antiforgery";
import { ServiceBase } from "./service-base";
import { ProfileOnboardingService } from "./profile-onboarding.service";
import { MemberFeedsService } from "./member-feeds.service";

const HEADER = "RequestVerificationToken";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function tokenResponse(token: string): Response {
  return json({ token, headerName: HEADER });
}

/** A fetch mock that serves tokens in order and hands every other call to `api`. */
function mockFetch(tokens: string[], api: (url: string, init: RequestInit) => Response) {
  const queue = [...tokens];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = String(input);
    if (url === ANTIFORGERY_TOKEN_URL) {
      const token = queue.shift();
      return token ? tokenResponse(token) : new Response(null, { status: 401, statusText: "Unauthorized" });
    }
    return api(url, init);
  });
  global.fetch = fetchMock as unknown as typeof fetch;
  return fetchMock;
}

function apiCalls(fetchMock: ReturnType<typeof mockFetch>) {
  return fetchMock.mock.calls.filter(([input]) => String(input) !== ANTIFORGERY_TOKEN_URL);
}

function tokenCalls(fetchMock: ReturnType<typeof mockFetch>) {
  return fetchMock.mock.calls.filter(([input]) => String(input) === ANTIFORGERY_TOKEN_URL);
}

function headerOf(call: unknown[]): string | null {
  return new Headers((call[1] as RequestInit).headers).get(HEADER);
}

describe("antiforgery", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("requiresAntiforgeryToken", () => {
    it.each([
      ["/api/profile/bio", "PUT", true],
      ["/api/profile/onboarding/start", "post", true],
      ["/api/member-feeds/3", "DELETE", true],
      ["/api/member-feeds", "POST", true],
      ["/api/member-feeds", "GET", false],
      ["/api/profiles-other", "POST", false],
      ["/api/feed-submission/submit", "PUT", false],
      ["https://example.com/api/profile/bio", "PUT", false],
    ])("%s %s -> %s", (url, method, expected) => {
      expect(requiresAntiforgeryToken(url, method)).toBe(expected);
    });
  });

  it("fetches the token once and reuses it", async () => {
    const fetchMock = mockFetch(["token-1"], () => json({}));

    await ServiceBase.put("/api/profile/bio", { bio: "Hi" });
    await ServiceBase.post("/api/member-feeds", { platform: "Blog", url: "https://example.com" });
    await ServiceBase.delete("/api/member-feeds/1", { reason: "old" });

    expect(tokenCalls(fetchMock)).toHaveLength(1);
    const calls = apiCalls(fetchMock);
    expect(calls).toHaveLength(3);
    calls.forEach((call) => expect(headerOf(call)).toBe("token-1"));
  });

  it("keeps the JSON content type on post, put and delete", async () => {
    const fetchMock = mockFetch(["token-1"], () => json({}));

    await ServiceBase.put("/api/profile/visibility", { visible: true });

    const init = apiCalls(fetchMock)[0][1] as RequestInit;
    expect(init.method).toBe("put");
    expect(init.body).toBe(JSON.stringify({ visible: true }));
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
  });

  it("attaches the token to the avatar upload without setting a content type", async () => {
    const fetchMock = mockFetch(["token-1"], () => json({ avatarUrl: "/a.png", hasCustomAvatar: true }));
    const file = new File(["x"], "avatar.png", { type: "image/png" });

    await ProfileOnboardingService.uploadAvatar(file);

    const init = apiCalls(fetchMock)[0][1] as RequestInit;
    expect(init.body).toBeInstanceOf(FormData);
    const headers = new Headers(init.headers);
    expect(headers.get(HEADER)).toBe("token-1");
    expect(headers.has("Content-Type")).toBe(false);
  });

  it("attaches the token to the avatar delete and the feed remove", async () => {
    const fetchMock = mockFetch(["token-1"], () => json({ avatarUrl: "/a.png", hasCustomAvatar: false }));

    await ProfileOnboardingService.removeAvatar();
    await MemberFeedsService.remove(7, "gone");

    const [avatarCall, feedCall] = apiCalls(fetchMock);
    expect(String(avatarCall[0])).toBe("/api/profile/avatar");
    expect((avatarCall[1] as RequestInit).method).toBe("delete");
    expect(headerOf(avatarCall)).toBe("token-1");
    expect(String(feedCall[0])).toBe("/api/member-feeds/7");
    expect((feedCall[1] as RequestInit).body).toBe(JSON.stringify({ reason: "gone" }));
    expect(headerOf(feedCall)).toBe("token-1");
  });

  it("does not touch requests to other APIs", async () => {
    const fetchMock = mockFetch(["token-1"], () => json({}));

    await ServiceBase.put("/api/feed-submission/submit", { feedUrl: "https://example.com" });
    await ServiceBase.get("/api/member-feeds");

    expect(tokenCalls(fetchMock)).toHaveLength(0);
    apiCalls(fetchMock).forEach((call) => expect(call[1] === undefined || headerOf(call) === null).toBe(true));
  });

  it("refetches the token and retries once when the token is rejected", async () => {
    let attempts = 0;
    const fetchMock = mockFetch(["stale", "fresh"], (_url, init) => {
      attempts++;
      return new Headers(init.headers).get(HEADER) === "fresh"
        ? json({})
        : new Response(null, { status: 400, statusText: "Bad Request" });
    });

    const response = await antiforgeryFetch("/api/profile/bio", { method: "put", body: "{}" });

    expect(response.ok).toBe(true);
    expect(attempts).toBe(2);
    expect(tokenCalls(fetchMock)).toHaveLength(2);
  });

  it("retries only once, then returns the rejection", async () => {
    const fetchMock = mockFetch(["one", "two", "three"], () =>
      json({ title: "Bad Request", status: 400 }, 400)
    );

    const response = await antiforgeryFetch("/api/profile/bio", { method: "put", body: "{}" });

    expect(response.status).toBe(400);
    expect(apiCalls(fetchMock)).toHaveLength(2);
    expect(tokenCalls(fetchMock)).toHaveLength(2);
  });

  it("does not retry an application error and keeps its message", async () => {
    const fetchMock = mockFetch(["token-1"], () => json({ error: "Bio must be 280 characters or fewer." }, 400));

    await expect(ProfileOnboardingService.updateBio("x")).rejects.toThrow("Bio must be 280 characters or fewer.");

    expect(apiCalls(fetchMock)).toHaveLength(1);
    expect(tokenCalls(fetchMock)).toHaveLength(1);
  });

  it("surfaces the existing error message when the token cannot be fetched", async () => {
    const fetchMock = mockFetch([], () => json({}));

    await expect(ProfileOnboardingService.setProfileVisible(true)).rejects.toThrow("Unauthorized");

    expect(apiCalls(fetchMock)).toHaveLength(0);
  });

  it("does not cache a failed token fetch", async () => {
    let tokens = 0;
    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      if (String(input) === ANTIFORGERY_TOKEN_URL) {
        tokens++;
        return tokens === 1 ? new Response(null, { status: 503, statusText: "Service Unavailable" }) : tokenResponse("later");
      }
      return json({});
    }) as unknown as typeof fetch;

    const first = await antiforgeryFetch("/api/profile/bio", { method: "put" });
    const second = await antiforgeryFetch("/api/profile/bio", { method: "put" });

    expect(first.status).toBe(503);
    expect(second.ok).toBe(true);
    expect(tokens).toBe(2);
  });
});
