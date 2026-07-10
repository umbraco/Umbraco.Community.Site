// Mirrors the DTOs on UmbracoCommunity.Web.Features.Announcements.Api.Models.

/** AnnouncementStatus byte values (see Models/Entities/AnnouncementStatus.cs). */
export const enum PostStatus {
  Pending = 0,
  Announced = 1,
  SkippedTooOld = 2,
  Suppressed = 3,
  Failed = 4,
}

/** AnnouncementTrigger byte values (see Models/Entities/AnnouncementTrigger.cs). */
export const enum AnnouncementTrigger {
  Auto = 0,
  Repost = 1,
  PostNow = 2,
  Backfill = 3,
  Reset = 4,
}

export interface PostListItem {
  sphereId: string;
  title: string;
  url: string;
  authorName: string | null;
  authorAvatarUrl: string | null;
  authorProfileUrl: string | null;
  publishedAtUtc: string;
  announcedUtc: string | null;
  status: number;
  attemptCount: number;
}

export interface PostListResponse {
  total: number;
  items: PostListItem[];
}

export interface AttemptItem {
  id: number;
  attemptedUtc: string;
  outcome: string;
  httpStatus: number | null;
  trigger: number;
  destination: string;
}

export interface PostDetail {
  sphereId: string;
  title: string;
  url: string;
  excerpt: string | null;
  authorName: string | null;
  authorAvatarUrl: string | null;
  authorProfileUrl: string | null;
  coverImageUrl: string | null;
  publishedAtUtc: string;
  firstSeenUtc: string;
  announcedUtc: string | null;
  fingerprint: string;
  status: number;
  attempts: AttemptItem[];
}

export interface RunListItem {
  id: number;
  runUtc: string;
  fetched: number;
  new: number;
  announced: number;
  skipped: number;
  failed: number;
  dryRun: boolean;
}

export interface RunListResponse {
  total: number;
  items: RunListItem[];
}

export interface SettingsResponse {
  recencyWindowDays: number;
  maxAnnouncementsPerCycle: number;
  dryRun: boolean;
  webhookConfigured: boolean;
  /** Effective poll cadence in minutes (fetch + announce cycle), floor already applied. */
  pollIntervalMinutes: number;
  /** Whether the host wired up on-demand polling — drives the "Poll now" button. */
  pollNowAvailable: boolean;
}

/** Result of a manual "poll now": the detection run recorded by the poll, when one was written. */
export interface PollNowResponse {
  run: RunListItem | null;
}

export interface DeliveryResultResponse {
  outcome: string;
  httpStatus: number | null;
  dryRun: boolean;
  status: number | null;
  announcedUtc: string | null;
}

export interface PostQuery {
  status?: number;
  search?: string;
  from?: string;
  to?: string;
  skip?: number;
  take?: number;
}
