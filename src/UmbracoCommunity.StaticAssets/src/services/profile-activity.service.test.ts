import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ANTIFORGERY_TOKEN_URL, resetAntiforgeryToken } from "./antiforgery";
import {
  MAX_ACTIVITY_IMAGE_BYTES,
  ProfileActivityEntryService,
  activityImageError,
  isLookupLink,
} from "./profile-activity.service";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

const added = {
  id: 7,
  kind: "blog",
  kindLabel: "Blog post",
  title: "My post",
  url: "https://example.dev/p",
  date: "2026-10-01",
  description: null,
  state: "Verified",
  rejectionReason: null,
  imageUrl: "/media/profile-activity/abc.png",
};

// Answers the antiforgery token request itself and records the API call.
function mockApi(apiResponse: () => Response) {
  const calls: Array<{ url: string; init?: RequestInit }> = [];
  global.fetch = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input) === ANTIFORGERY_TOKEN_URL) {
      return Promise.resolve(json({ token: "test-token", headerName: "RequestVerificationToken" }));
    }
    calls.push({ url: String(input), init });
    return Promise.resolve(apiResponse());
  }) as typeof fetch;
  return calls;
}

describe("ProfileActivityEntryService.add", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("posts the entry and its picture as a multipart form with the antiforgery token", async () => {
    const calls = mockApi(() => json(added));
    const image = new File(["x"], "me.png", { type: "image/png" });

    const result = await ProfileActivityEntryService.add({
      kind: "blog",
      title: "My post",
      url: "https://example.dev/p",
      date: "2026-10-01",
      description: "",
      image,
    });

    expect(result.imageUrl).toBe("/media/profile-activity/abc.png");
    const { url, init } = calls[0];
    expect(url).toBe("/api/profile/activity");
    expect(init?.method).toBe("post");
    const body = init?.body as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect(body.get("title")).toBe("My post");
    expect(body.get("date")).toBe("2026-10-01");
    expect((body.get("image") as File).name).toBe("me.png");
    // The browser sets the multipart boundary; the token is added.
    const headers = new Headers(init?.headers);
    expect(headers.has("Content-Type")).toBe(false);
    expect(headers.get("RequestVerificationToken")).toBe("test-token");
  });

  it("leaves the picture out when there is none", async () => {
    const calls = mockApi(() => json({ ...added, imageUrl: null }));

    await ProfileActivityEntryService.add({ kind: "blog", title: "T", url: "https://example.dev/p", date: "2026-10-01", description: "" });

    expect((calls[0].init?.body as FormData).has("image")).toBe(false);
  });

  it("throws the server's message", async () => {
    mockApi(() => json({ error: "The picture must be a JPEG, PNG, GIF or WebP image." }, 400));

    await expect(
      ProfileActivityEntryService.add({ kind: "blog", title: "T", url: "https://example.dev/p", date: "2026-10-01", description: "" })
    ).rejects.toThrow("JPEG, PNG, GIF or WebP");
  });
});

describe("ProfileActivityEntryService.update", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const changes = { kind: "blog", title: "T", url: "https://example.dev/p", date: "2026-10-01", description: "" };

  it("puts the changes as a multipart form to the entry's address with the antiforgery token", async () => {
    const calls = mockApi(() => json(added));
    const image = new File(["x"], "new.png", { type: "image/png" });

    await ProfileActivityEntryService.update(7, { ...changes, image });

    const { url, init } = calls[0];
    expect(url).toBe("/api/profile/activity/7");
    expect(init?.method).toBe("put");
    const body = init?.body as FormData;
    expect((body.get("image") as File).name).toBe("new.png");
    expect(body.has("removeImage")).toBe(false);
    expect(new Headers(init?.headers).get("RequestVerificationToken")).toBe("test-token");
  });

  it("asks to remove the picture only when no new one is sent", async () => {
    const calls = mockApi(() => json(added));

    await ProfileActivityEntryService.update(7, { ...changes, removeImage: true });
    await ProfileActivityEntryService.update(7, changes);

    expect((calls[0].init?.body as FormData).get("removeImage")).toBe("true");
    expect((calls[1].init?.body as FormData).has("removeImage")).toBe(false);
  });

  it("throws the server's message", async () => {
    mockApi(() => json({ error: "A moderator removed this entry, so it can't be changed." }, 409));

    await expect(ProfileActivityEntryService.update(7, changes)).rejects.toThrow("A moderator removed this entry");
  });
});

describe("ProfileActivityEntryService.lookUp", () => {
  beforeEach(() => {
    resetAntiforgeryToken();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("posts the link and returns what the page offers", async () => {
    const calls = mockApi(() => json({ kind: "videos", title: "A talk", date: "2026-09-30", description: null, imageUrl: null }));

    const details = await ProfileActivityEntryService.lookUp("https://example.dev/talk");

    expect(details).toEqual({ kind: "videos", title: "A talk", date: "2026-09-30", description: null, imageUrl: null });
    expect(calls[0].url).toBe("/api/profile/activity/preview");
    expect(calls[0].init?.method).toBe("post");
    expect(JSON.parse(calls[0].init?.body as string)).toEqual({ url: "https://example.dev/talk" });
  });

  it("gives an empty answer instead of failing", async () => {
    const empty = { kind: null, title: null, date: null, description: null, imageUrl: null };
    mockApi(() => json({ error: "nope" }, 403));
    expect(await ProfileActivityEntryService.lookUp("https://example.dev/a")).toEqual(empty);

    global.fetch = vi.fn(() => Promise.reject(new Error("offline"))) as typeof fetch;
    expect(await ProfileActivityEntryService.lookUp("https://example.dev/a")).toEqual(empty);
  });
});

describe("isLookupLink", () => {
  it("accepts https addresses only", () => {
    expect(isLookupLink("https://example.dev/a")).toBe(true);
    expect(isLookupLink(" https://example.dev ")).toBe(true);
    expect(isLookupLink("http://example.dev/a")).toBe(false);
    expect(isLookupLink("example.dev")).toBe(false);
    expect(isLookupLink("")).toBe(false);
  });
});

describe("activityImageError", () => {
  it("accepts the four picture formats up to the limit", () => {
    for (const type of ["image/jpeg", "image/png", "image/gif", "image/webp"]) {
      expect(activityImageError(new File(["x"], "p", { type }))).toBeNull();
    }
    expect(activityImageError(new File([new Uint8Array(MAX_ACTIVITY_IMAGE_BYTES)], "p.png", { type: "image/png" }))).toBeNull();
  });

  it("refuses other types and pictures over the limit", () => {
    expect(activityImageError(new File(["<svg/>"], "p.svg", { type: "image/svg+xml" }))).toContain("JPEG, PNG, GIF or WebP");
    expect(activityImageError(new File([new Uint8Array(MAX_ACTIVITY_IMAGE_BYTES + 1)], "p.png", { type: "image/png" }))).toContain(
      "2 MB"
    );
  });
});
