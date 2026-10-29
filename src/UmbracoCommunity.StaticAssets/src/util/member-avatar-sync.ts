import {
  PROFILE_AVATAR_CHANGED_EVENT,
  type ProfileAvatarChangedDetail,
} from "../services/profile-onboarding.service.js";

/** Marks an img that shows the signed-in member's avatar (the header avatar). */
export const MEMBER_AVATAR_SELECTOR = "img[data-member-avatar]";

/** Points every member avatar img under `root` at the new URL. Does nothing when there are none. */
export function updateMemberAvatars(avatarUrl: string, root: ParentNode = document): void {
  if (!avatarUrl) return;
  root.querySelectorAll<HTMLImageElement>(MEMBER_AVATAR_SELECTOR).forEach((img) => {
    img.src = avatarUrl;
  });
}

/**
 * Keeps the member avatar imgs in step with avatar uploads and removals made anywhere on
 * the page. Returns a function that stops listening.
 */
export function listenForMemberAvatarChanges(target: Window = window): () => void {
  const handler = (event: Event) => {
    const detail = (event as CustomEvent<ProfileAvatarChangedDetail>).detail;
    if (detail?.avatarUrl) {
      updateMemberAvatars(detail.avatarUrl);
    }
  };
  target.addEventListener(PROFILE_AVATAR_CHANGED_EVENT, handler);
  return () => target.removeEventListener(PROFILE_AVATAR_CHANGED_EVENT, handler);
}
