import { PostStatus, AnnouncementTrigger } from "./api/blog-announcements-types.js";

export const STATUS_LABELS: Record<number, string> = {
  [PostStatus.Pending]: "Pending",
  [PostStatus.Announced]: "Announced",
  [PostStatus.SkippedTooOld]: "Skipped (too old)",
  [PostStatus.Suppressed]: "Suppressed",
  [PostStatus.Failed]: "Failed",
};

// uui-tag colours: positive (green), warning (amber), danger (red), default (grey).
export function statusColor(status: number): "positive" | "warning" | "danger" | "default" {
  switch (status) {
    case PostStatus.Announced:
      return "positive";
    case PostStatus.Pending:
      return "warning";
    case PostStatus.Failed:
      return "danger";
    default:
      return "default"; // SkippedTooOld, Suppressed
  }
}

export const TRIGGER_LABELS: Record<number, string> = {
  [AnnouncementTrigger.Auto]: "Auto",
  [AnnouncementTrigger.Repost]: "Repost",
  [AnnouncementTrigger.PostNow]: "Post now",
  [AnnouncementTrigger.Backfill]: "Backfill",
  [AnnouncementTrigger.Reset]: "Reset",
};

/**
 * Parses a server timestamp defensively: the API stores UTC, so a string without an explicit
 * timezone designator is treated as UTC (browsers would otherwise parse it as local time).
 */
export function parseServerDate(iso: string): Date {
  const hasZone = /[zZ]$|[+-]\d{2}:\d{2}$/.test(iso);
  return new Date(hasZone ? iso : `${iso}Z`);
}

/** e.g. "3 days ago", "just now". Returns "" for null. */
export function relativeTime(iso: string | null): string {
  if (!iso) return "";
  const then = parseServerDate(iso).getTime();
  const diffMs = Date.now() - then;
  const sec = Math.round(diffMs / 1000);
  if (sec < 60) return "just now";
  const min = Math.round(sec / 60);
  if (min < 60) return `${min} minute${min === 1 ? "" : "s"} ago`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `${hr} hour${hr === 1 ? "" : "s"} ago`;
  const days = Math.round(hr / 24);
  if (days < 30) return `${days} day${days === 1 ? "" : "s"} ago`;
  const months = Math.round(days / 30);
  if (months < 12) return `${months} month${months === 1 ? "" : "s"} ago`;
  const years = Math.round(months / 12);
  return `${years} year${years === 1 ? "" : "s"} ago`;
}

export function absoluteTime(iso: string | null): string {
  return iso ? parseServerDate(iso).toLocaleString() : "";
}

/** e.g. 15 -> "15 minutes", 360 -> "6 hours", 90 -> "1 hour 30 minutes". */
export function formatMinutes(minutes: number): string {
  const minutePart = (m: number) => `${m} minute${m === 1 ? "" : "s"}`;
  if (minutes < 60) return minutePart(minutes);
  const h = Math.floor(minutes / 60);
  const hourPart = `${h} hour${h === 1 ? "" : "s"}`;
  const rest = minutes % 60;
  return rest === 0 ? hourPart : `${hourPart} ${minutePart(rest)}`;
}
