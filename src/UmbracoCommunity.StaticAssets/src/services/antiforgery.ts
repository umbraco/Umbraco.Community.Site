/**
 * Antiforgery protection for the signed-in member APIs. The server requires a request token
 * in the RequestVerificationToken header on every POST, PUT and DELETE to these APIs. The
 * token is fetched once from a member-only endpoint, kept in memory, and attached
 * automatically by antiforgeryFetch (and so by ServiceBase.post/put/delete).
 */

/** Same-origin API paths whose mutating requests need the token. */
export const PROTECTED_API_PREFIXES = ["/api/profile", "/api/member-feeds"] as const;

export const ANTIFORGERY_TOKEN_URL = "/api/antiforgery/token";

/** ASP.NET Core's default antiforgery header, used if the server does not name one. */
export const DEFAULT_ANTIFORGERY_HEADER_NAME = "RequestVerificationToken";

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS", "TRACE"]);

interface AntiforgeryToken {
  token: string;
  headerName: string;
}

/** Thrown when the token endpoint answers with an error, carrying that response. */
class TokenUnavailableError extends Error {
  constructor(readonly response: Response) {
    super(response.statusText);
  }
}

let tokenPromise: Promise<AntiforgeryToken> | null = null;

/** Forgets the cached token so the next protected request fetches a fresh one. */
export function resetAntiforgeryToken(): void {
  tokenPromise = null;
}

/** The path of a same-origin URL, or null for any other origin. */
function sameOriginPath(url: string): string | null {
  if (url.startsWith("/") && !url.startsWith("//")) {
    return url.split(/[?#]/)[0];
  }
  try {
    const parsed = new URL(url);
    return parsed.origin === window.location.origin ? parsed.pathname : null;
  } catch {
    return null;
  }
}

/** True when a request to this URL with this method must carry the token. */
export function requiresAntiforgeryToken(url: string, method: string): boolean {
  if (SAFE_METHODS.has(method.toUpperCase())) {
    return false;
  }

  const path = sameOriginPath(url)?.toLowerCase();
  if (!path) {
    return false;
  }

  return PROTECTED_API_PREFIXES.some((prefix) => path === prefix || path.startsWith(`${prefix}/`));
}

async function fetchToken(): Promise<AntiforgeryToken> {
  const response = await fetch(ANTIFORGERY_TOKEN_URL, { cache: "no-store" });
  if (!response.ok) {
    throw new TokenUnavailableError(response);
  }

  const body = await response.json();
  return {
    token: body.token,
    headerName: body.headerName || DEFAULT_ANTIFORGERY_HEADER_NAME,
  };
}

function getToken(): Promise<AntiforgeryToken> {
  if (!tokenPromise) {
    tokenPromise = fetchToken().catch((error) => {
      // Don't cache a failure; the next request tries again.
      tokenPromise = null;
      throw error;
    });
  }
  return tokenPromise;
}

function withToken(init: RequestInit, token: AntiforgeryToken): RequestInit {
  const headers = new Headers(init.headers);
  headers.set(token.headerName, token.token);
  return { ...init, headers };
}

/**
 * True when a 400 looks like an antiforgery rejection rather than an application error. The
 * member APIs report their own validation problems as JSON with an "error" message; an
 * antiforgery rejection has no such message.
 */
async function isAntiforgeryRejection(response: Response): Promise<boolean> {
  if (response.status !== 400) {
    return false;
  }
  try {
    const body = await response.clone().json();
    return !(body && typeof body.error === "string" && body.error);
  } catch {
    return true;
  }
}

async function fetchWithToken(url: string, init: RequestInit): Promise<Response> {
  let token: AntiforgeryToken;
  try {
    token = await getToken();
  } catch (error) {
    // e.g. signed out: hand back the token endpoint's own response (401) so callers report
    // it the same way they report any other failed request.
    if (error instanceof TokenUnavailableError) {
      return error.response;
    }
    throw error;
  }
  return fetch(url, withToken(init, token));
}

/**
 * fetch() that attaches the antiforgery token to mutating requests to the member APIs. If
 * such a request is rejected for its token (for example after signing in again in another
 * tab), the token is fetched again and the request retried once. Every other request is
 * passed straight to fetch().
 */
export async function antiforgeryFetch(url: string, init: RequestInit = {}): Promise<Response> {
  if (!requiresAntiforgeryToken(url, init.method ?? "GET")) {
    return fetch(url, init);
  }

  const response = await fetchWithToken(url, init);
  if (!(await isAntiforgeryRejection(response))) {
    return response;
  }

  resetAntiforgeryToken();
  return fetchWithToken(url, init);
}
