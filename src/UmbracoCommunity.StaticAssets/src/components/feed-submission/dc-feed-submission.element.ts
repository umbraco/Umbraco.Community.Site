import { LitElement, html, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import {
  FeedSubmissionService,
  type FeedSubmissionPost,
  type FeedSubmissionResult,
  type FeedSubmissionStatus,
} from "../../services/feed-submission.service.js";

const elementName = "dc-feed-submission";

type Phase = "idle" | "loading" | "previewed" | "submitting" | "submitted" | "error";

interface FeedSubmissionFormValues {
  feedUrl: string;
  name: string;
  githubUsername: string;
  honeypot: string;
}

const emptyForm: FeedSubmissionFormValues = {
  feedUrl: "",
  name: "",
  githubUsername: "",
  honeypot: "",
};

@customElement(elementName)
export class FeedSubmissionElement extends LitElement {
  @state()
  private _phase: Phase = "idle";

  @state()
  private _posts: FeedSubmissionPost[] = [];

  @state()
  private _formValues: FeedSubmissionFormValues = { ...emptyForm };

  @state()
  private _errorMessage = "";

  @state()
  private _submissionResult?: FeedSubmissionResult;

  // Render in light DOM so the global .dc-community-blogs__* card styles and the
  // feed-submission-block.css form styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  #readForm(form: HTMLFormElement): FeedSubmissionFormValues {
    const data = new FormData(form);
    return {
      feedUrl: (data.get("feedUrl") as string | null)?.trim() ?? "",
      name: (data.get("name") as string | null)?.trim() ?? "",
      githubUsername: (data.get("githubUsername") as string | null)?.trim() ?? "",
      honeypot: (data.get("company_website") as string | null) ?? "",
    };
  }

  async #handleSubmitForm(event: Event) {
    event.preventDefault();
    const values = this.#readForm(event.currentTarget as HTMLFormElement);
    this._formValues = values;
    await this.#runPreview();
  }

  async #runPreview() {
    const { feedUrl, name, githubUsername, honeypot } = this._formValues;
    this._phase = "loading";
    this._errorMessage = "";
    try {
      this._posts = await FeedSubmissionService.preview(
        feedUrl,
        name || undefined,
        githubUsername || undefined,
        honeypot
      );
      this._phase = "previewed";
    } catch (error) {
      this._errorMessage =
        error instanceof Error ? error.message : "Something went wrong. Please try again.";
      this._phase = "error";
    }
  }

  async #handleSubmitFeed() {
    const { feedUrl, name, githubUsername, honeypot } = this._formValues;
    this._phase = "submitting";
    this._errorMessage = "";
    try {
      this._submissionResult = await FeedSubmissionService.submit(
        feedUrl,
        name || undefined,
        githubUsername || undefined,
        honeypot
      );
      this._phase = "submitted";
    } catch (error) {
      this._errorMessage =
        error instanceof Error ? error.message : "Something went wrong. Please try again.";
      this._phase = "error";
    }
  }

  #backToForm() {
    this._phase = "idle";
    this._errorMessage = "";
  }

  #formatDate(value: string): { iso: string; display: string } {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return { iso: value, display: value };
    }
    return {
      iso: date.toISOString().slice(0, 10),
      display: date.toLocaleDateString(undefined, {
        day: "numeric",
        month: "long",
        year: "numeric",
      }),
    };
  }

  #excerpt(content?: string): string {
    if (!content) return "";
    return content.replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
  }

  #renderForm(disabled: boolean): TemplateResult {
    const values = this._formValues;
    return html`
      <form class="dc-feed-submission__form" @submit=${this.#handleSubmitForm} novalidate>
        <div class="dc-feed-submission__field">
          <label for="dc-feed-submission-url">Feed URL</label>
          <input
            id="dc-feed-submission-url"
            name="feedUrl"
            type="url"
            inputmode="url"
            placeholder="https://example.com/rss.xml"
            required
            autocomplete="off"
            .value=${values.feedUrl}
            ?disabled=${disabled}
          />
        </div>

        <div class="dc-feed-submission__field">
          <label for="dc-feed-submission-name">Name <span class="dc-feed-submission__optional">(optional)</span></label>
          <input
            id="dc-feed-submission-name"
            name="name"
            type="text"
            autocomplete="off"
            .value=${values.name}
            ?disabled=${disabled}
          />
        </div>

        <div class="dc-feed-submission__field">
          <label for="dc-feed-submission-github">GitHub username <span class="dc-feed-submission__optional">(optional)</span></label>
          <input
            id="dc-feed-submission-github"
            name="githubUsername"
            type="text"
            autocomplete="off"
            .value=${values.githubUsername}
            ?disabled=${disabled}
          />
        </div>

        <div class="dc-feed-submission__honeypot" aria-hidden="true">
          <label for="dc-feed-submission-company">Company website</label>
          <input
            id="dc-feed-submission-company"
            name="company_website"
            type="text"
            tabindex="-1"
            autocomplete="off"
          />
        </div>

        <div class="dc-feed-submission__actions">
          <button class="btn is-blue" type="submit" ?disabled=${disabled}>
            ${disabled ? "Loading preview…" : "Preview feed"}
          </button>
        </div>
      </form>
    `;
  }

  // Proxies a remote image through our own origin so it loads from 'self' with no CSP img-src relaxation —
  // this is a live preview of an arbitrary feed's own domain, so the image can't be pre-localized like the
  // persisted Community Blogs grid does. The proxy itself gracefully 404s (invalid host, oversized, non-image
  // content-type, etc.); #onImageError below falls back to the placeholder when that happens.
  #proxyImage(url: string): string {
    return `/api/feed-submission/image-proxy?url=${encodeURIComponent(url)}`;
  }

  #onImageError(event: Event, placeholder: string) {
    const img = event.target as HTMLImageElement;
    if (img.src !== new URL(placeholder, location.href).href) {
      img.src = placeholder;
    }
  }

  #renderCard(post: FeedSubmissionPost): TemplateResult {
    const { iso, display } = this.#formatDate(post.publishedAt);
    const excerpt = this.#excerpt(post.content);
    const authorName = post.author?.name;
    // Sphere's preview endpoint accepts a `github` field but doesn't yet honour it in the returned
    // avatarUrl (verified directly against the live API — same avatarUrl with or without it), so a
    // submitted GitHub username is enforced client-side instead. Uses avatars.githubusercontent.com
    // directly (not github.com/{user}.png, which 302s there) since the image proxy disables redirects.
    const githubUsername = this._formValues.githubUsername;
    const avatarUrl = githubUsername
      ? `https://avatars.githubusercontent.com/${encodeURIComponent(githubUsername)}`
      : post.author?.avatarUrl;
    const coverPlaceholder = "/img/community-blogs/placeholder-cover.svg";
    const avatarPlaceholder = "/img/community-blogs/placeholder-avatar.svg";
    return html`
      <a
        class="dc-community-blogs__card"
        href=${post.url ?? "#"}
        target="_blank"
        rel="noopener noreferrer"
      >
        <figure class="dc-community-blogs__media">
          <img
            src=${post.coverImageUrl ? this.#proxyImage(post.coverImageUrl) : coverPlaceholder}
            alt=""
            loading="lazy"
            @error=${(event: Event) => this.#onImageError(event, coverPlaceholder)}
          />
        </figure>
        <div class="dc-community-blogs__content">
          <img
            class="dc-community-blogs__avatar"
            src=${avatarUrl ? this.#proxyImage(avatarUrl) : avatarPlaceholder}
            alt=""
            loading="lazy"
            @error=${(event: Event) => this.#onImageError(event, avatarPlaceholder)}
          />
          <h3 class="dc-community-blogs__title">${post.title}</h3>
          ${when(
            excerpt,
            () => html`<p class="dc-community-blogs__teaser">${excerpt}</p>`
          )}
          <div class="dc-community-blogs__meta">
            <time datetime=${iso}>${display}</time>
            ${when(
              authorName,
              () => html`<span class="dc-community-blogs__author">by ${authorName}</span>`
            )}
          </div>
        </div>
      </a>
    `;
  }

  #renderPreview(): TemplateResult {
    return html`
      <div class="dc-feed-submission__actions">
        <button class="btn" type="button" @click=${this.#runPreview}>
          Check again
        </button>
        <button class="btn is-blue" type="button" @click=${this.#handleSubmitFeed}>
          Submit for inclusion
        </button>
      </div>
      ${this._posts.length === 0
        ? html`<p class="dc-feed-submission__empty">
            We couldn't find any posts in that feed. Double-check the URL and try again.
          </p>`
        : html`
            <div class="dc-community-blogs">
              <div class="dc-community-blogs__grid">
                ${this._posts.map((post) => this.#renderCard(post))}
              </div>
            </div>
          `}
    `;
  }

  #renderLoading(message: string): TemplateResult {
    return html`
      <div class="dc-feed-submission__status" role="status" aria-live="polite">
        <div class="dc-feed-submission__spinner"></div>
        <p>${message}</p>
      </div>
    `;
  }

  #renderSubmitted(): TemplateResult {
    const status = this._submissionResult?.status ?? "pending";
    const isRejected = status === "rejected";
    const { heading, message } = this.#submittedCopy(status);
    return html`
      <div
        class="dc-feed-submission__status dc-feed-submission__status--${isRejected ? "rejected" : "success"}"
        role="status"
      >
        <h3>${heading}</h3>
        <p>${message}</p>
        <div class="dc-feed-submission__actions">
          <button class="btn" type="button" @click=${this.#backToForm}>
            Submit another feed
          </button>
        </div>
      </div>
    `;
  }

  #submittedCopy(status: FeedSubmissionStatus): { heading: string; message: string } {
    switch (status) {
      case "already_exists":
        return {
          heading: "Already listed",
          message: "This feed is already in our system.",
        };
      case "rejected":
        return {
          heading: "Not accepted",
          message: "This feed couldn't be accepted.",
        };
      case "pending":
      default:
        return {
          heading: "Thanks!",
          message: "Your feed is pending review.",
        };
    }
  }

  #renderError(): TemplateResult {
    return html`
      <div class="dc-feed-submission__status dc-feed-submission__status--error" role="alert">
        <p>${this._errorMessage}</p>
        <div class="dc-feed-submission__actions">
          <button class="btn is-blue" type="button" @click=${this.#backToForm}>
            Back to form
          </button>
        </div>
      </div>
    `;
  }

  render() {
    switch (this._phase) {
      case "loading":
        return html`${this.#renderForm(true)}${this.#renderLoading("Fetching your feed…")}`;
      case "previewed":
        return this.#renderPreview();
      case "submitting":
        return this.#renderLoading("Submitting your feed…");
      case "submitted":
        return this.#renderSubmitted();
      case "error":
        return this.#renderError();
      case "idle":
      default:
        return this.#renderForm(false);
    }
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: FeedSubmissionElement;
  }
}
