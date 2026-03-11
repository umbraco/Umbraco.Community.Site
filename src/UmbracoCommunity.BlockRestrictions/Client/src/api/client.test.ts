import { vi, type Mock } from "vitest";
import {
  setAuthConfig,
  getAllowedBlocks,
  getRule,
  saveRule,
  deleteRule,
  getElementTypes,
  getBlockDataTypes,
  exportDbRulesZip,
  exportDiskFilesZip,
  uploadZip,
} from "./client.js";

const API_BASE = "/umbraco/umbracocommunityblockrestrictions/api/v1";

/** Helper to create a mock Response. */
function mockResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json" },
    ...init,
  });
}

function mockTextResponse(text: string, init?: ResponseInit): Response {
  return new Response(text, {
    status: 200,
    headers: { "Content-Type": "text/plain" },
    ...init,
  });
}

let fetchMock: Mock;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  // Reset auth config between tests.
  setAuthConfig({});
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ─── Auth configuration ──────────────────────────────────────────────────────

describe("setAuthConfig / fetchWithAuth", () => {
  it("adds a static token string to the Authorization header", async () => {
    setAuthConfig({ token: "my-static-token" });
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers["Authorization"]).toBe("Bearer my-static-token");
  });

  it("calls an async token function and uses the result", async () => {
    const tokenFn = vi.fn().mockResolvedValue("async-token-value");
    setAuthConfig({ token: tokenFn });
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    expect(tokenFn).toHaveBeenCalled();
    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers["Authorization"]).toBe("Bearer async-token-value");
  });

  it("does not add Authorization header when no token is configured", async () => {
    setAuthConfig({});
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers["Authorization"]).toBeUndefined();
  });

  it("passes credentials through to fetch", async () => {
    setAuthConfig({ credentials: "include" });
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    const [, options] = fetchMock.mock.calls[0];
    expect(options.credentials).toBe("include");
  });

  it("defaults credentials to same-origin", async () => {
    setAuthConfig({});
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    const [, options] = fetchMock.mock.calls[0];
    expect(options.credentials).toBe("same-origin");
  });

  it("prepends the base URL to all requests", async () => {
    setAuthConfig({ baseUrl: "https://example.com" });
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    const [url] = fetchMock.mock.calls[0];
    expect(url).toMatch(/^https:\/\/example\.com/);
  });
});

// ─── getAllowedBlocks ────────────────────────────────────────────────────────

describe("getAllowedBlocks", () => {
  it("returns the parsed response on success", async () => {
    const body = {
      documentTypeAlias: "blogPost",
      allowedBlocks: ["richText"],
      allowedContentElementTypeKeys: ["key-1"],
      hasRestrictions: true,
      inheritedFromAncestor: false,
    };
    fetchMock.mockResolvedValue(mockResponse(body));

    const result = await getAllowedBlocks("node-key");

    expect(result).toEqual(body);
  });

  it("returns null on 404", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 404 }));

    const result = await getAllowedBlocks("node-key");

    expect(result).toBeNull();
  });

  it("throws on other HTTP errors", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(getAllowedBlocks("node-key")).rejects.toThrow(
      "Failed to fetch allowed blocks",
    );
  });

  it("appends contentTypeKey and parentKey as query params when provided", async () => {
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key", "ct-key", "parent-key");

    const [url] = fetchMock.mock.calls[0];
    expect(url).toContain("contentTypeKey=ct-key");
    expect(url).toContain("parentKey=parent-key");
  });

  it("omits query params when not provided", async () => {
    fetchMock.mockResolvedValue(mockResponse({ hasRestrictions: false }));

    await getAllowedBlocks("node-key");

    const [url] = fetchMock.mock.calls[0];
    expect(url).toBe(`${API_BASE}/allowed-blocks/node-key`);
    expect(url).not.toContain("?");
  });
});

// ─── getRule ─────────────────────────────────────────────────────────────────

describe("getRule", () => {
  it("returns the parsed rule on success", async () => {
    const rule = {
      documentTypeKey: "doc-key",
      allowedBlockAliases: ["richText", "image"],
    };
    fetchMock.mockResolvedValue(mockTextResponse(JSON.stringify(rule)));

    const result = await getRule("doc-key");

    expect(result).toEqual(rule);
  });

  it("returns null on empty response body", async () => {
    fetchMock.mockResolvedValue(mockTextResponse(""));

    const result = await getRule("doc-key");

    expect(result).toBeNull();
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(getRule("doc-key")).rejects.toThrow("Failed to fetch rule");
  });
});

// ─── saveRule ────────────────────────────────────────────────────────────────

describe("saveRule", () => {
  it("sends PUT with the correct body", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    await saveRule("doc-key", ["richText", "image"]);

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toContain(`${API_BASE}/rules/doc-key`);
    expect(options.method).toBe("PUT");
    expect(JSON.parse(options.body)).toEqual({
      allowedBlockAliases: ["richText", "image"],
    });
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(saveRule("doc-key", ["richText"])).rejects.toThrow(
      "Failed to save rule",
    );
  });
});

// ─── deleteRule ──────────────────────────────────────────────────────────────

describe("deleteRule", () => {
  it("sends DELETE request", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    await deleteRule("doc-key");

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toContain(`${API_BASE}/rules/doc-key`);
    expect(options.method).toBe("DELETE");
  });

  it("does not throw on 404", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 404, statusText: "Not Found" }),
    );

    await expect(deleteRule("doc-key")).resolves.toBeUndefined();
  });

  it("throws on other HTTP errors", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(deleteRule("doc-key")).rejects.toThrow(
      "Failed to delete rule",
    );
  });
});

// ─── getElementTypes ─────────────────────────────────────────────────────────

describe("getElementTypes", () => {
  it("returns the parsed array on success", async () => {
    const elements = [
      { key: "k1", alias: "richText", name: "Rich Text", icon: "icon-text" },
    ];
    fetchMock.mockResolvedValue(mockResponse(elements));

    const result = await getElementTypes();

    expect(result).toEqual(elements);
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(getElementTypes()).rejects.toThrow(
      "Failed to fetch element types",
    );
  });
});

// ─── getBlockDataTypes ───────────────────────────────────────────────────────

describe("getBlockDataTypes", () => {
  it("returns the parsed array on success", async () => {
    const dataTypes = [
      {
        key: "dt1",
        name: "Content Blocks",
        editorType: "Umbraco.BlockGrid",
        contentElementTypeKeys: ["k1", "k2"],
      },
    ];
    fetchMock.mockResolvedValue(mockResponse(dataTypes));

    const result = await getBlockDataTypes();

    expect(result).toEqual(dataTypes);
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(getBlockDataTypes()).rejects.toThrow(
      "Failed to fetch block data types",
    );
  });
});

// ─── exportDbRulesZip ─────────────────────────────────────────────────────

describe("exportDbRulesZip", () => {
  it("calls the correct endpoint and returns a Blob", async () => {
    fetchMock.mockResolvedValue(new Response("zip-bytes", { status: 200 }));

    const result = await exportDbRulesZip();

    const [url] = fetchMock.mock.calls[0];
    expect(url).toContain(`${API_BASE}/file-import/export-db`);
    expect(result.size).toBeGreaterThan(0);
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(exportDbRulesZip()).rejects.toThrow(
      "Failed to export DB rules",
    );
  });
});

// ─── exportDiskFilesZip ───────────────────────────────────────────────────

describe("exportDiskFilesZip", () => {
  it("calls the correct endpoint and returns a Blob", async () => {
    fetchMock.mockResolvedValue(new Response("zip-bytes", { status: 200 }));

    const result = await exportDiskFilesZip();

    const [url] = fetchMock.mock.calls[0];
    expect(url).toContain(`${API_BASE}/file-import/export-files`);
    expect(result.size).toBeGreaterThan(0);
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    await expect(exportDiskFilesZip()).rejects.toThrow(
      "Failed to export disk files",
    );
  });
});

// ─── uploadZip ────────────────────────────────────────────────────────────

describe("uploadZip", () => {
  it("sends a POST with FormData containing the file", async () => {
    const responseBody = { filesWritten: 3, errors: [] };
    fetchMock.mockResolvedValue(mockResponse(responseBody));

    const file = new File(["zip-data"], "rules.zip", {
      type: "application/zip",
    });
    const result = await uploadZip(file);

    expect(result).toEqual(responseBody);
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toContain(`${API_BASE}/file-import/upload`);
    expect(options.method).toBe("POST");
    expect(options.body).toBeInstanceOf(FormData);
    expect((options.body as FormData).get("file")).toBeInstanceOf(File);
  });

  it("does not set Content-Type header (browser sets multipart boundary)", async () => {
    fetchMock.mockResolvedValue(
      mockResponse({ filesWritten: 0, errors: [] }),
    );

    const file = new File(["zip-data"], "rules.zip");
    await uploadZip(file);

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers["Content-Type"]).toBeUndefined();
  });

  it("includes Authorization header when token is configured", async () => {
    setAuthConfig({ token: "my-token" });
    fetchMock.mockResolvedValue(
      mockResponse({ filesWritten: 1, errors: [] }),
    );

    const file = new File(["zip-data"], "rules.zip");
    await uploadZip(file);

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers["Authorization"]).toBe("Bearer my-token");
  });

  it("returns errors from the response", async () => {
    const responseBody = {
      filesWritten: 1,
      errors: [{ alias: "bad.json", error: "Invalid JSON" }],
    };
    fetchMock.mockResolvedValue(mockResponse(responseBody));

    const file = new File(["zip-data"], "rules.zip");
    const result = await uploadZip(file);

    expect(result.errors).toHaveLength(1);
    expect(result.errors[0].alias).toBe("bad.json");
  });

  it("throws on HTTP error", async () => {
    fetchMock.mockResolvedValue(
      new Response(null, { status: 500, statusText: "Internal Server Error" }),
    );

    const file = new File(["zip-data"], "rules.zip");
    await expect(uploadZip(file)).rejects.toThrow("Failed to upload zip");
  });
});
