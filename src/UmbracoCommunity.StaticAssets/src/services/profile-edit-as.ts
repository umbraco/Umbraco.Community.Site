import { isMemberApi } from "./antiforgery";

/**
 * Edit-as for profile admins. On the account page of another member's profile (`/account?edit-as={handle}`), the page
 * carries that member's handle in `data-profile-edit-as`; every call to the member APIs from the page then names them
 * in the `X-Profile-Edit-As` header, so the server acts on their profile (and checks the caller may). Nothing is kept
 * between page loads: the attribute is read on each request.
 */
export const EDIT_AS_HEADER = "X-Profile-Edit-As";

/** The handle of the member being edited, or null on a member's own pages. */
export function editAsHandle(): string | null {
  return document.querySelector<HTMLElement>("[data-profile-edit-as]")?.dataset.profileEditAs?.trim() || null;
}

/** `init` with the edit-as header added, for a member API call while editing someone; otherwise null. */
export function withEditAs(url: string, init: RequestInit = {}): RequestInit | null {
  const handle = editAsHandle();
  if (!handle || !isMemberApi(url)) return null;

  const headers = new Headers(init.headers);
  headers.set(EDIT_AS_HEADER, handle);
  return { ...init, headers };
}
