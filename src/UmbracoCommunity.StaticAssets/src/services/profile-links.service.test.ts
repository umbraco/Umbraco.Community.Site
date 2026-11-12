import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ANTIFORGERY_TOKEN_URL, resetAntiforgeryToken } from "./antiforgery";
import { ProfileLinksService } from "./profile-links.service";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

// Answers the antiforgery token request itself and returns a fresh response for the API call.
function mockApi(apiResponse: () => Response) {
  const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) =>
    Promise.resolve(
      String(input) === ANTIFORGERY_TOKEN_URL
        ? json({ token: "test-token", headerName: "RequestVerificationToken" })
        : apiResponse()
    )
  );
  global.fetch = fetchMock as typeof fetch;
  return fetchMock;
}

describe("ProfileLinksService", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("reads the suggestions", async () => {
    const suggestions = [{ url: "https://social.example/@octocat", platform: "Mastodon", name: "octocat", kind: "Account" }];
    const fetchMock = mockApi(() => json(suggestions));

    const result = await ProfileLinksService.suggestions();

    expect(result).toEqual(suggestions);
    expect(fetchMock.mock.calls.some(([input]) => String(input) === "/api/profile/links/suggestions")).toBe(true);
  });

  it("sends a not-me with a POST that carries the antiforgery token", async () => {
    const fetchMock = mockApi(() => new Response(null, { status: 200 }));

    await ProfileLinksService.notMe("https://social.example/@someone");

    const [, init] = fetchMock.mock.calls.find(([input]) => String(input) === "/api/profile/links/not-me")!;
    expect(init?.method).toBe("post");
    expect(JSON.parse(init?.body as string)).toEqual({ url: "https://social.example/@someone" });
    expect(new Headers(init?.headers).get("RequestVerificationToken")).toBe("test-token");
  });

  it("reports the server's error message", async () => {
    mockApi(() => json({ error: "Please provide a valid https URL." }, 400));

    await expect(ProfileLinksService.notMe("http://nope")).rejects.toThrow("Please provide a valid https URL.");
  });
});
