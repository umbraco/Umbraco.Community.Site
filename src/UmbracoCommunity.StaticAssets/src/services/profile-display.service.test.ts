import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ANTIFORGERY_TOKEN_URL, resetAntiforgeryToken } from "./antiforgery";
import { ProfileDisplayService } from "./profile-display.service";

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

describe("ProfileDisplayService", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("saves the choices with a PUT that carries the antiforgery token", async () => {
    const choices = { hiddenSections: ["blog"], hiddenItemIds: ["b1"], pins: [{ id: "v1", reason: "Proud" }] };
    const fetchMock = mockApi(() => json(choices));

    const result = await ProfileDisplayService.save(choices);

    expect(result).toEqual(choices);
    const [url, init] = fetchMock.mock.calls.find(([input]) => String(input) === "/api/profile/display")!;
    expect(url).toBe("/api/profile/display");
    expect(init?.method).toBe("put");
    expect(JSON.parse(init?.body as string)).toEqual(choices);
    expect(new Headers(init?.headers).get("RequestVerificationToken")).toBe("test-token");
  });

  it("sends only the id and reason of each pin", async () => {
    const choices = { hiddenSections: [], hiddenItemIds: [], pins: [{ id: "b1", reason: null, title: "An old post" }] };
    const fetchMock = mockApi(() => json(choices));

    await ProfileDisplayService.save(choices);

    const [, init] = fetchMock.mock.calls.find(([input]) => String(input) === "/api/profile/display")!;
    expect(JSON.parse(init?.body as string).pins).toEqual([{ id: "b1", reason: null }]);
  });

  it("reports the server's error message", async () => {
    mockApi(() => json({ error: "You can pin up to 3 items." }, 400));

    await expect(ProfileDisplayService.save({ hiddenSections: [], hiddenItemIds: [], pins: [] })).rejects.toThrow(
      "You can pin up to 3 items."
    );
  });
});
