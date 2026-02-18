const API_BASE = "/umbraco/umbracocommunityblockrestrictions/api/v1";

export interface AllowedBlocksResponse {
  documentTypeAlias: string;
  allowedBlocks: string[];
  allowedContentElementTypeKeys: string[];
  hasRestrictions: boolean;
  inheritedFromAncestor: boolean;
}

export interface BlockRestrictionRuleDto {
  documentTypeKey: string;
  allowedBlockAliases: string[];
}

export interface ElementTypeInfo {
  key: string;
  alias: string;
  name: string;
  icon: string;
}

export interface BlockDataTypeInfo {
  key: string;
  name: string;
  editorType: string;
  contentElementTypeKeys: string[];
}

export interface ApiAuthConfig {
  token?: string | (() => Promise<string | undefined>);
  baseUrl?: string;
  credentials?: RequestCredentials;
}

let _authConfig: ApiAuthConfig = {};

export function setAuthConfig(config: ApiAuthConfig) {
  _authConfig = config;
}

async function resolveToken(): Promise<string | undefined> {
  if (!_authConfig.token) return undefined;
  if (typeof _authConfig.token === "function") {
    return _authConfig.token();
  }
  return _authConfig.token;
}

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

export async function getAllowedBlocks(nodeKey: string): Promise<AllowedBlocksResponse | null> {
  const response = await fetchWithAuth(`${API_BASE}/allowed-blocks/${nodeKey}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to fetch allowed blocks: ${response.status} ${response.statusText}`);
  return response.json();
}

export async function getRule(docTypeKey: string): Promise<BlockRestrictionRuleDto | null> {
  const response = await fetchWithAuth(`${API_BASE}/rules/${docTypeKey}`);
  if (!response.ok) throw new Error(`Failed to fetch rule: ${response.statusText}`);
  const text = await response.text();
  if (!text) return null;
  return JSON.parse(text);
}

export async function saveRule(docTypeKey: string, allowedBlockAliases: string[]): Promise<void> {
  const response = await fetchWithAuth(`${API_BASE}/rules/${docTypeKey}`, {
    method: "PUT",
    body: JSON.stringify({ allowedBlockAliases }),
  });
  if (!response.ok) throw new Error(`Failed to save rule: ${response.statusText}`);
}

export async function deleteRule(docTypeKey: string): Promise<void> {
  const response = await fetchWithAuth(`${API_BASE}/rules/${docTypeKey}`, {
    method: "DELETE",
  });
  if (!response.ok && response.status !== 404) {
    throw new Error(`Failed to delete rule: ${response.statusText}`);
  }
}

export async function getElementTypes(): Promise<ElementTypeInfo[]> {
  const response = await fetchWithAuth(`${API_BASE}/element-types`);
  if (!response.ok) throw new Error(`Failed to fetch element types: ${response.statusText}`);
  return response.json();
}

export async function getBlockDataTypes(): Promise<BlockDataTypeInfo[]> {
  const response = await fetchWithAuth(`${API_BASE}/block-data-types`);
  if (!response.ok) throw new Error(`Failed to fetch block data types: ${response.statusText}`);
  return response.json();
}
