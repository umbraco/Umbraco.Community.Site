import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ANTIFORGERY_TOKEN_URL, resetAntiforgeryToken } from "./antiforgery";
import { ProfileOnboardingService, PROFILE_AVATAR_CHANGED_EVENT } from "./profile-onboarding.service";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

// Answers the antiforgery token request itself and returns a fresh response for the API call,
// so a response body is never read twice.
function mockApi(apiResponse: () => Response) {
  global.fetch = vi.fn((input: RequestInfo | URL) =>
    Promise.resolve(
      String(input) === ANTIFORGERY_TOKEN_URL
        ? json({ token: "test-token", headerName: "RequestVerificationToken" })
        : apiResponse()
    )
  ) as typeof fetch;
}

describe("ProfileOnboardingService avatar changes", () => {
  let listener: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    resetAntiforgeryToken();
    listener = vi.fn();
    window.addEventListener(PROFILE_AVATAR_CHANGED_EVENT, listener);
  });

  afterEach(() => {
    window.removeEventListener(PROFILE_AVATAR_CHANGED_EVENT, listener);
    vi.restoreAllMocks();
  });

  it("announces the new avatar after an upload", async () => {
    mockApi(() => json({ avatarUrl: "/media/new/me.png", hasCustomAvatar: true }));

    const result = await ProfileOnboardingService.uploadAvatar(new File(["x"], "me.png", { type: "image/png" }));

    expect(result).toEqual({ avatarUrl: "/media/new/me.png", hasCustomAvatar: true });
    expect(listener).toHaveBeenCalledOnce();
    expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({ avatarUrl: "/media/new/me.png" });
  });

  it("announces the fallback avatar after a removal", async () => {
    mockApi(() => json({ avatarUrl: "https://github.com/octocat.png", hasCustomAvatar: false }));

    await ProfileOnboardingService.removeAvatar();

    expect(listener).toHaveBeenCalledOnce();
    expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({ avatarUrl: "https://github.com/octocat.png" });
  });

  it("announces nothing when the upload fails", async () => {
    mockApi(() => json({ error: "That file type isn't supported." }, 400));

    await expect(ProfileOnboardingService.uploadAvatar(new File(["x"], "me.txt"))).rejects.toThrow(
      "That file type isn't supported."
    );

    expect(listener).not.toHaveBeenCalled();
  });

  it("announces nothing when the removal fails", async () => {
    mockApi(() => new Response(null, { status: 500, statusText: "Server Error" }));

    await expect(ProfileOnboardingService.removeAvatar()).rejects.toThrow("Server Error");

    expect(listener).not.toHaveBeenCalled();
  });
});

describe("ProfileOnboardingService profile details", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const saved = {
    displayName: "The Octocat",
    hasCustomDisplayName: false,
    defaultDisplayName: "The Octocat",
    headline: "umbraco",
    hasCustomHeadline: false,
    gitHubHeadline: "umbraco",
    bio: "Bio from GitHub",
    hasCustomBio: false,
    gitHubBio: "Bio from GitHub",
  };

  function apiCall(): { url: string; init: RequestInit } {
    const call = vi
      .mocked(global.fetch)
      .mock.calls.find(([input]) => String(input) !== ANTIFORGERY_TOKEN_URL)!;
    return { url: String(call[0]), init: call[1] as RequestInit };
  }

  it("saves a trimmed headline and returns the saved details", async () => {
    mockApi(() => json({ ...saved, headline: "Umbraco developer", hasCustomHeadline: true }));

    const result = await ProfileOnboardingService.updateHeadline("  Umbraco developer ");

    expect(result.headline).toBe("Umbraco developer");
    const { url, init } = apiCall();
    expect(url).toBe("/api/profile/headline");
    expect(init.method).toBe("put");
    expect(JSON.parse(init.body as string)).toEqual({ headline: "Umbraco developer" });
  });

  it("sends an empty headline as null", async () => {
    mockApi(() => json(saved));

    await ProfileOnboardingService.updateHeadline("   ");

    expect(JSON.parse(apiCall().init.body as string)).toEqual({ headline: null });
  });

  it("saves a trimmed display name", async () => {
    mockApi(() => json({ ...saved, displayName: "Octo", hasCustomDisplayName: true }));

    const result = await ProfileOnboardingService.updateDisplayName(" Octo ");

    expect(result.displayName).toBe("Octo");
    const { url, init } = apiCall();
    expect(url).toBe("/api/profile/display-name");
    expect(init.method).toBe("put");
    expect(JSON.parse(init.body as string)).toEqual({ displayName: "Octo" });
  });

  it("sends an empty display name as null and returns the GitHub name in use", async () => {
    mockApi(() => json(saved));

    const result = await ProfileOnboardingService.updateDisplayName("");

    expect(result).toEqual(saved);
    expect(JSON.parse(apiCall().init.body as string)).toEqual({ displayName: null });
  });

  it("sends an empty bio as null and returns the GitHub bio in use", async () => {
    mockApi(() => json(saved));

    const result = await ProfileOnboardingService.updateBio("");

    expect(result).toEqual(saved);
    const { url, init } = apiCall();
    expect(url).toBe("/api/profile/bio");
    expect(JSON.parse(init.body as string)).toEqual({ bio: null });
  });

  it("throws the server's error message when saving fails", async () => {
    mockApi(() => json({ error: "Headline must be 100 characters or fewer." }, 400));

    await expect(ProfileOnboardingService.updateHeadline("x")).rejects.toThrow(
      "Headline must be 100 characters or fewer."
    );
  });
});
