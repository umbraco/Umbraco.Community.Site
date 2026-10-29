import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ProfileOnboardingService, PROFILE_AVATAR_CHANGED_EVENT } from "./profile-onboarding.service";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

describe("ProfileOnboardingService avatar changes", () => {
  let listener: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    listener = vi.fn();
    window.addEventListener(PROFILE_AVATAR_CHANGED_EVENT, listener);
  });

  afterEach(() => {
    window.removeEventListener(PROFILE_AVATAR_CHANGED_EVENT, listener);
    vi.restoreAllMocks();
  });

  it("announces the new avatar after an upload", async () => {
    global.fetch = vi.fn().mockResolvedValue(json({ avatarUrl: "/media/new/me.png", hasCustomAvatar: true }));

    const result = await ProfileOnboardingService.uploadAvatar(new File(["x"], "me.png", { type: "image/png" }));

    expect(result).toEqual({ avatarUrl: "/media/new/me.png", hasCustomAvatar: true });
    expect(listener).toHaveBeenCalledOnce();
    expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({ avatarUrl: "/media/new/me.png" });
  });

  it("announces the fallback avatar after a removal", async () => {
    global.fetch = vi.fn().mockResolvedValue(json({ avatarUrl: "https://github.com/octocat.png", hasCustomAvatar: false }));

    await ProfileOnboardingService.removeAvatar();

    expect(listener).toHaveBeenCalledOnce();
    expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({ avatarUrl: "https://github.com/octocat.png" });
  });

  it("announces nothing when the upload fails", async () => {
    global.fetch = vi.fn().mockResolvedValue(json({ error: "That file type isn't supported." }, 400));

    await expect(ProfileOnboardingService.uploadAvatar(new File(["x"], "me.txt"))).rejects.toThrow(
      "That file type isn't supported."
    );

    expect(listener).not.toHaveBeenCalled();
  });

  it("announces nothing when the removal fails", async () => {
    global.fetch = vi.fn().mockResolvedValue(new Response(null, { status: 500, statusText: "Server Error" }));

    await expect(ProfileOnboardingService.removeAvatar()).rejects.toThrow("Server Error");

    expect(listener).not.toHaveBeenCalled();
  });
});
