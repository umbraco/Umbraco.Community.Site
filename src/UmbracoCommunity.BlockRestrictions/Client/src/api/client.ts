/**
 * API client for the Block Restrictions backoffice API.
 *
 * This module provides typed functions for all API endpoints and handles
 * authentication by consuming the Umbraco backoffice auth context.
 *
 * Auth lifecycle:
 *   1. Property editors and workspace view consume UMB_AUTH_CONTEXT
 *   2. They call setAuthConfig() with the token function, base URL, and credentials
 *   3. All subsequent fetchWithAuth() calls include the Bearer token automatically
 *
 * The auth config is stored at module level (singleton). The property editors guard
 * against undefined authContext callbacks to prevent clobbering the stored token
 * when the context is unprovided during element disconnection.
 */

/** Base URL for all API endpoints (matches the BackOfficeRoute in the .NET controller). */
const API_BASE = "/umbraco/umbracocommunityblockrestrictions/api/v1";

// ─── Response/Request types ───────────────────────────────────────────────────

/** Response from GET allowed-blocks/{nodeKey} — the resolved restrictions for a content node. */
export interface AllowedBlocksResponse {
  /** The alias of the document type where the rule is defined. */
  documentTypeAlias: string;
  /** The allowed element type aliases (as stored in the database). */
  allowedBlocks: string[];
  /** The resolved GUIDs of the allowed content element types. */
  allowedContentElementTypeKeys: string[];
  /** Whether any restrictions are in effect. False = fail-open, all blocks allowed. */
  hasRestrictions: boolean;
  /** Whether the restriction was inherited from an ancestor content node. */
  inheritedFromAncestor: boolean;
}

/** Response from GET rules/{docTypeKey} — a single restriction rule. */
export interface BlockRestrictionRuleDto {
  documentTypeKey: string;
  allowedBlockAliases: string[];
}

/** An Umbraco element type (content type where IsElement = true). */
export interface ElementTypeInfo {
  key: string;
  alias: string;
  name: string;
  icon: string;
}

/** A restricted Block Grid or Block List data type with its configured blocks. */
export interface BlockDataTypeInfo {
  key: string;
  name: string;
  editorType: string;
  contentElementTypeKeys: string[];
}

// ─── Auth configuration ───────────────────────────────────────────────────────

/** Configuration for authenticated API requests. */
export interface ApiAuthConfig {
  /** Either a static token string or a function that returns a token (from Umbraco's auth context). */
  token?: string | (() => Promise<string | undefined>);
  /** The base URL for API requests (from the Umbraco auth context). */
  baseUrl?: string;
  /** The credentials mode for fetch requests. */
  credentials?: RequestCredentials;
}

/** Module-level auth config singleton. Set once by the first element that loads. */
let _authConfig: ApiAuthConfig = {};

/** Sets the auth configuration. Called by property editors and workspace view on context consumption. */
export function setAuthConfig(config: ApiAuthConfig) {
  _authConfig = config;
}

/**
 * Resolves the auth token. Supports both static strings and async token functions
 * (Umbraco provides a function that returns a fresh token on each call).
 */
async function resolveToken(): Promise<string | undefined> {
  if (!_authConfig.token) return undefined;
  if (typeof _authConfig.token === "function") {
    return _authConfig.token();
  }
  return _authConfig.token;
}

/**
 * Authenticated fetch wrapper. Adds the Authorization header and merges
 * the base URL and credentials from the auth config.
 */
async function fetchWithAuth(url: string, options: RequestInit = {}): Promise<Response> {
  const token = await resolveToken();
  const baseUrl = _authConfig.baseUrl ?? "";
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }
  const response = await fetch(`${baseUrl}${url}`, {
    ...options,
    headers,
    credentials: _authConfig.credentials ?? "same-origin",
  });
  return response;
}

// ─── API functions ────────────────────────────────────────────────────────────

/**
 * Resolves the effective block restrictions for a content node.
 * Called by property editors when they load in the content editor.
 *
 * For existing content, only nodeKey is needed — the server walks the content tree.
 * For new content (node doesn't exist yet), contentTypeKey and parentKey provide
 * fallback context so the server can check the document type's rule directly
 * and/or walk up from the parent node.
 *
 * Returns null for 404 (node not found and no fallback available).
 */
export async function getAllowedBlocks(
  nodeKey: string,
  contentTypeKey?: string,
  parentKey?: string,
): Promise<AllowedBlocksResponse | null> {
  const params = new URLSearchParams();
  if (contentTypeKey) params.set("contentTypeKey", contentTypeKey);
  if (parentKey) params.set("parentKey", parentKey);
  const query = params.toString() ? `?${params.toString()}` : "";
  const response = await fetchWithAuth(`${API_BASE}/allowed-blocks/${nodeKey}${query}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to fetch allowed blocks: ${response.status} ${response.statusText}`);
  return response.json();
}

/**
 * Gets the restriction rule for a specific document type.
 * Called by the workspace view to load the current config on the Blocks tab.
 * Returns null if no rule exists (the document type inherits from ancestors).
 */
export async function getRule(docTypeKey: string): Promise<BlockRestrictionRuleDto | null> {
  const response = await fetchWithAuth(`${API_BASE}/rules/${docTypeKey}`);
  if (!response.ok) throw new Error(`Failed to fetch rule: ${response.statusText}`);
  const text = await response.text();
  if (!text) return null;
  return JSON.parse(text);
}

/** Creates or updates a restriction rule for a document type. */
export async function saveRule(docTypeKey: string, allowedBlockAliases: string[]): Promise<void> {
  const response = await fetchWithAuth(`${API_BASE}/rules/${docTypeKey}`, {
    method: "PUT",
    body: JSON.stringify({ allowedBlockAliases }),
  });
  if (!response.ok) throw new Error(`Failed to save rule: ${response.statusText}`);
}

/** Deletes a restriction rule (returning the document type to inheritance). */
export async function deleteRule(docTypeKey: string): Promise<void> {
  const response = await fetchWithAuth(`${API_BASE}/rules/${docTypeKey}`, {
    method: "DELETE",
  });
  // 404 is acceptable — the rule may have already been deleted.
  if (!response.ok && response.status !== 404) {
    throw new Error(`Failed to delete rule: ${response.statusText}`);
  }
}

/** Lists all element types for the workspace view's block type checklist. */
export async function getElementTypes(): Promise<ElementTypeInfo[]> {
  const response = await fetchWithAuth(`${API_BASE}/element-types`);
  if (!response.ok) throw new Error(`Failed to fetch element types: ${response.statusText}`);
  return response.json();
}

/** Lists restricted block data types for the workspace view's "Filter by data type" dropdown. */
export async function getBlockDataTypes(): Promise<BlockDataTypeInfo[]> {
  const response = await fetchWithAuth(`${API_BASE}/block-data-types`);
  if (!response.ok) throw new Error(`Failed to fetch block data types: ${response.statusText}`);
  return response.json();
}
