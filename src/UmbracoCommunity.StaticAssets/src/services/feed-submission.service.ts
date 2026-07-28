import { ServiceBase } from "./service-base";

export interface FeedSubmissionAuthor {
  name?: string;
  profileUrl?: string;
  avatarUrl?: string;
  type?: string;
}

export interface FeedSubmissionPost {
  id: string;
  type: string;
  platform?: string;
  title?: string;
  url?: string;
  content?: string;
  coverImageUrl?: string;
  publishedAt: string;
  author?: FeedSubmissionAuthor;
}

export type FeedSubmissionStatus = "pending" | "already_exists" | "rejected";

export interface FeedSubmissionResult {
  id: string | null;
  url: string;
  name: string | null;
  github: string | null;
  status: FeedSubmissionStatus;
  submittedAt: string | null;
}

/** Whether a feed URL is already listed, has a pending submission, or neither. */
export type FeedListingStatus = "listed" | "pending" | "none";

export interface FeedPreviewResult {
  posts: FeedSubmissionPost[];
  status: FeedListingStatus;
}

interface FeedSubmissionRequest {
  feedUrl: string;
  name?: string;
  githubUsername?: string;
  honeypot?: string;
}

export class FeedSubmissionService extends ServiceBase {
  private static readonly BASE_URL = "/api/feed-submission";

  /**
   * Fetches a preview of the posts a feed URL would produce (without persisting anything), plus
   * whether the feed is already listed, pending review, or neither.
   */
  static async preview(
    feedUrl: string,
    name?: string,
    githubUsername?: string,
    honeypot?: string
  ): Promise<FeedPreviewResult> {
    const response = await ServiceBase.post(
      `${this.BASE_URL}/preview`,
      this.#buildBody(feedUrl, name, githubUsername, honeypot)
    );
    if (!response.ok) {
      throw new Error(await this.#errorMessage(response));
    }
    return response.json();
  }

  /**
   * Submits a feed URL for manual review.
   */
  static async submit(
    feedUrl: string,
    name?: string,
    githubUsername?: string,
    honeypot?: string
  ): Promise<FeedSubmissionResult> {
    const response = await ServiceBase.put(
      `${this.BASE_URL}/submit`,
      this.#buildBody(feedUrl, name, githubUsername, honeypot)
    );
    if (!response.ok) {
      throw new Error(await this.#errorMessage(response));
    }
    return response.json();
  }

  static #buildBody(
    feedUrl: string,
    name?: string,
    githubUsername?: string,
    honeypot?: string
  ): FeedSubmissionRequest {
    return {
      feedUrl,
      name,
      githubUsername,
      honeypot: honeypot ?? "",
    };
  }

  static async #errorMessage(response: Response): Promise<string> {
    try {
      const body = await response.json();
      if (body && typeof body.error === "string" && body.error) {
        return body.error;
      }
    } catch {
      // Fall through to statusText when the body isn't the expected JSON shape.
    }
    return response.statusText;
  }
}
