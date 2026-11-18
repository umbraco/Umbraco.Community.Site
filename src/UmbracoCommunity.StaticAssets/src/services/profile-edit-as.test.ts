import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ANTIFORGERY_TOKEN_URL, resetAntiforgeryToken } from "./antiforgery";
import { EDIT_AS_HEADER, editAsHandle } from "./profile-edit-as";
import { ServiceBase } from "./service-base";
import { ProfileOnboardingService } from "./profile-onboarding.service";

function mockFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) =>
    String(input) === ANTIFORGERY_TOKEN_URL
      ? new Response(JSON.stringify({ token: "t", headerName: "RequestVerificationToken" }), { status: 200 })
      : new Response("{}", { status: 200 }),
  );
  global.fetch = fetchMock as unknown as typeof fetch;
  return fetchMock;
}

function apiCall(fetchMock: ReturnType<typeof mockFetch>, url: string) {
  return fetchMock.mock.calls.find(([input]) => String(input) === url);
}

function editAsOf(call: unknown[] | undefined): string | null {
  return new Headers((call?.[1] as RequestInit | undefined)?.headers).get(EDIT_AS_HEADER);
}

describe("profile edit-as", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    document.body.innerHTML = "";
  });

  it("reads the edited member's handle from the page", () => {
    document.body.innerHTML = `<article data-profile-edit-as="hubot"></article>`;
    expect(editAsHandle()).toBe("hubot");

    document.body.innerHTML = `<article></article>`;
    expect(editAsHandle()).toBeNull();
  });

  it("names the edited member on every member API call, reads and changes alike", async () => {
    document.body.innerHTML = `<article data-profile-edit-as="hubot"></article>`;
    const fetchMock = mockFetch();

    await ServiceBase.get("/api/member-feeds");
    await ProfileOnboardingService.updateBio("Hello");

    expect(editAsOf(apiCall(fetchMock, "/api/member-feeds"))).toBe("hubot");
    const put = apiCall(fetchMock, "/api/profile/bio");
    expect(editAsOf(put)).toBe("hubot");
    expect(new Headers((put![1] as RequestInit).headers).get("RequestVerificationToken")).toBe("t");
  });

  it("never sends the header anywhere but the member APIs", async () => {
    document.body.innerHTML = `<article data-profile-edit-as="hubot"></article>`;
    const fetchMock = mockFetch();

    await ServiceBase.get("/api/sessionize/sessions");
    await ServiceBase.get("https://elsewhere.example/api/profile");

    expect(editAsOf(apiCall(fetchMock, "/api/sessionize/sessions"))).toBeNull();
    expect(editAsOf(apiCall(fetchMock, "https://elsewhere.example/api/profile"))).toBeNull();
  });

  it("leaves calls on a member's own pages as they were", async () => {
    const fetchMock = mockFetch();

    await ServiceBase.get("/api/member-feeds");
    await ProfileOnboardingService.updateBio("Hello");

    expect(fetchMock).toHaveBeenCalledWith("/api/member-feeds");
    expect(editAsOf(apiCall(fetchMock, "/api/profile/bio"))).toBeNull();
  });
});
