import { css, html, nothing } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { unsafeHTML } from "lit/directives/unsafe-html.js";
import { DcDialogBaseElement } from "../dialog/dialog-base.element.js";
import { DcDialogHandler } from "../dialog/dialog.handler.js";
import {
  SessionizeSession,
  SessionizeSpeaker,
  SessionizeCategory,
  SessionizeSchedule,
} from "../../services/sessionize.service.js";
import { SessionizeSpeakerDialogElement } from "./sessionize-speaker-dialog.element.js";
import { iconClock, iconMapPin, iconShare, iconCheck, iconCopy, iconLinkedin, iconBluesky, iconMastodon } from "../../svg/lucide-icons.js";

const elementName = "dc-sessionize-session-dialog";

@customElement(elementName)
export class SessionizeSessionDialogElement extends DcDialogBaseElement {
  @property({ type: Object })
  session?: SessionizeSession;

  @property({ type: String })
  timezone?: string;

  @property({ type: Array })
  categories: SessionizeCategory[] = [];

  @property({ type: Array })
  schedule: SessionizeSchedule[] = [];

  @state()
  private _showCopiedFeedback = false;

  #dialogHandler = new DcDialogHandler();
  #copiedTimeout?: ReturnType<typeof setTimeout>;

  #getCategoryItemName(itemId: number): { name: string; categoryTitle: string } | null {
    for (const category of this.categories) {
      const item = category.items.find((i) => i.id === itemId);
      if (item) {
        return { name: item.name, categoryTitle: category.title };
      }
    }
    return null;
  }

  #onHashtagClick(itemId: number, itemName: string, e: Event) {
    e.stopPropagation();
    this.close();
    // Dispatch custom event for the program component to handle
    this.dispatchEvent(
      new CustomEvent("filter-select", {
        detail: { id: itemId, name: itemName },
        bubbles: true,
        composed: true,
      })
    );
  }

  #linkifyText(text: string): string {
    // Escape HTML entities first to prevent XSS
    const escaped = text
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");

    // URL regex pattern - matches http, https, and www URLs
    const urlPattern = /(https?:\/\/[^\s<]+|www\.[^\s<]+)/gi;

    return escaped.replace(urlPattern, (url) => {
      const href = url.startsWith("www.") ? `https://${url}` : url;
      return `<a href="${href}" target="_blank" rel="noopener noreferrer">${url}</a>`;
    });
  }

  #parseAsUtc(timeString: string): Date {
    // Ensure UTC interpretation by adding Z if no timezone indicator present
    let dateTimeStr = timeString;
    if (!dateTimeStr.endsWith("Z") && !dateTimeStr.includes("+") && !dateTimeStr.includes("-", 10)) {
      dateTimeStr += "Z";
    }
    return new Date(dateTimeStr);
  }

  #formatTime(timeString?: string): string {
    if (!timeString) return "";
    try {
      const date = this.#parseAsUtc(timeString);
      const options: Intl.DateTimeFormatOptions = {
        hour: "2-digit",
        minute: "2-digit",
      };
      if (this.timezone) {
        options.timeZone = this.timezone;
      }
      return date.toLocaleTimeString("en-GB", options);
    } catch {
      return timeString;
    }
  }

  #formatDate(timeString?: string): string {
    if (!timeString) return "";
    try {
      const date = this.#parseAsUtc(timeString);
      const options: Intl.DateTimeFormatOptions = {
        weekday: "short",
        day: "numeric",
        month: "short",
      };
      if (this.timezone) {
        options.timeZone = this.timezone;
      }
      return date.toLocaleDateString("en-GB", options);
    } catch {
      return "";
    }
  }

  #formatDateRange(): string {
    if (!this.session?.startsAt) return "";
    const start = this.#formatTime(this.session.startsAt);
    const end = this.#formatTime(this.session.endsAt);
    const date = this.#formatDate(this.session.startsAt);
    const timeRange = end ? `${start} - ${end}` : start;
    return date ? `${date}, ${timeRange}` : timeRange;
  }

  #openSpeakerDialog(speaker: SessionizeSpeaker, e: Event) {
    e.stopPropagation();
    // Close current dialog first
    this.close();
    // Open speaker dialog
    setTimeout(() => {
      const dialog = new SessionizeSpeakerDialogElement();
      dialog.speaker = speaker;
      dialog.schedule = this.schedule;
      dialog.timezone = this.timezone;
      this.#dialogHandler.open(dialog);
    }, 100);
  }

  #getShareUrl(): string {
    const url = new URL(window.location.href);
    if (this.session) {
      url.searchParams.set("session", this.session.id);
    }
    return url.toString();
  }

  #isMobileOrTablet(): boolean {
    // Check for touch capability and mobile user agent
    const hasTouch = "ontouchstart" in window || navigator.maxTouchPoints > 0;
    const mobileRegex = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i;
    return hasTouch && mobileRegex.test(navigator.userAgent);
  }

  async #shareSession() {
    if (!this.session) return;

    const shareUrl = this.#getShareUrl();

    // Only use Web Share API on mobile/tablet devices where it provides a good UX
    if (this.#isMobileOrTablet() && navigator.share) {
      const shareData = {
        title: this.session.title,
        text: this.session.speakers?.length
          ? `${this.session.title} by ${this.session.speakers.map(s => s.fullName).join(", ")}`
          : this.session.title,
        url: shareUrl,
      };

      try {
        await navigator.share(shareData);
        return;
      } catch (err) {
        // User cancelled or share failed, fall through to clipboard
        if ((err as Error).name === "AbortError") {
          return; // User cancelled, don't show copied feedback
        }
      }
    }

    // Copy to clipboard (desktop default and mobile fallback)
    await this.#copyToClipboard(shareUrl);
  }

  async #copyToClipboard(text: string) {
    // Try Clipboard API first (need to check if it's allowed)
    if (navigator.clipboard && window.isSecureContext) {
      try {
        await navigator.clipboard.writeText(text);
        this.#showCopied();
        return;
      } catch (err) {
        // Clipboard API failed, fall through to execCommand
        console.log("Clipboard API failed, trying execCommand:", err);
      }
    }

    // Fallback to execCommand
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.top = "0";
    textArea.style.left = "0";
    textArea.style.width = "2em";
    textArea.style.height = "2em";
    textArea.style.padding = "0";
    textArea.style.border = "none";
    textArea.style.outline = "none";
    textArea.style.boxShadow = "none";
    textArea.style.background = "transparent";
    textArea.style.opacity = "0";
    textArea.setAttribute("readonly", "");

    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    textArea.setSelectionRange(0, text.length);

    const success = document.execCommand("copy");
    document.body.removeChild(textArea);

    if (success) {
      this.#showCopied();
    } else {
      console.error("execCommand copy also failed");
    }
  }

  #showCopied() {
    this._showCopiedFeedback = true;
    if (this.#copiedTimeout) {
      clearTimeout(this.#copiedTimeout);
    }
    this.#copiedTimeout = setTimeout(() => {
      this._showCopiedFeedback = false;
    }, 2000);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this.#copiedTimeout) {
      clearTimeout(this.#copiedTimeout);
    }
  }

  #renderSidebar() {
    const hasSpeakers = this.session?.speakers?.length;
    const hasMeta = this.session?.startsAt || this.session?.room;
    const hasTags = this.session?.categoryItems?.length && this.categories.length;

    if (!hasSpeakers && !hasMeta && !hasTags) return nothing;

    return html`
      <aside class="session-sidebar">
        ${hasSpeakers ? html`
          <div class="sidebar-section speakers-section">
            <h3>Speakers</h3>
            <div class="speakers-list">
              ${this.session!.speakers.map(
                (speaker) => html`
                  <button
                    class="speaker-card"
                    @click=${(e: Event) => this.#openSpeakerDialog(speaker, e)}
                    title="View ${speaker.fullName}'s profile"
                  >
                    ${when(
                      speaker.profilePicture,
                      () => html`
                        <img
                          class="speaker-photo"
                          src=${speaker.profilePicture!}
                          alt=""
                        />
                      `,
                      () => html`
                        <div class="speaker-photo speaker-photo-placeholder">
                          ${speaker.firstName?.[0] ?? ""}${speaker.lastName?.[0] ?? ""}
                        </div>
                      `
                    )}
                    <span class="speaker-name">${speaker.fullName}</span>
                    ${when(
                      speaker.tagLine,
                      () => html`<span class="speaker-tagline">${speaker.tagLine}</span>`
                    )}
                  </button>
                `
              )}
            </div>
          </div>
        ` : nothing}

        ${hasMeta ? html`
          <div class="sidebar-section meta-section">
            <h3>When & Where</h3>
            <div class="sidebar-meta">
              ${when(
                this.session?.startsAt,
                () => html`
                  <div class="meta-row">
                    <span class="meta-icon">${iconClock}</span>
                    <span>${this.#formatDateRange()}</span>
                  </div>
                `
              )}
              ${when(
                this.session?.room,
                () => html`
                  <div class="meta-row">
                    <span class="meta-icon">${iconMapPin}</span>
                    <span>${this.session!.room}</span>
                  </div>
                `
              )}
            </div>
          </div>
        ` : nothing}

        ${this.#renderSidebarHashtags()}
      </aside>
    `;
  }

  #renderSidebarHashtags() {
    if (!this.session?.categoryItems?.length || !this.categories.length) return nothing;

    const tags = this.session.categoryItems
      .map((id) => {
        const info = this.#getCategoryItemName(id);
        if (!info) return null;
        if (info.name.toLowerCase().includes("minute") ||
            info.name.toLowerCase().includes("regular talk")) {
          return null;
        }
        return { id, name: info.name };
      })
      .filter((t): t is { id: number; name: string } => t !== null);

    if (!tags.length) return nothing;

    return html`
      <div class="sidebar-section tags-section">
        <h3>Topics</h3>
        <div class="sidebar-tags">
          ${tags.map(
            (tag) => html`
              <button
                class="hashtag"
                @click=${(e: Event) => this.#onHashtagClick(tag.id, tag.name, e)}
                title="Filter by ${tag.name}"
              >
                #${tag.name}
              </button>
            `
          )}
        </div>
      </div>
    `;
  }

  #renderLinks() {
    const links: Array<{ label: string; url: string }> = [];

    if (this.session?.liveUrl) {
      links.push({ label: "Watch Live", url: this.session.liveUrl });
    }
    if (this.session?.recordingUrl) {
      links.push({ label: "Watch Recording", url: this.session.recordingUrl });
    }

    if (!links.length) return nothing;

    return html`
      <div class="session-links">
        ${links.map(
          (link) => html`
            <a
              href=${link.url}
              target="_blank"
              rel="noopener noreferrer"
              class="session-link"
            >
              ${link.label}
            </a>
          `
        )}
      </div>
    `;
  }

  #getShareText(): string {
    if (!this.session) return "";
    const speakers = this.session.speakers?.length
      ? ` by ${this.session.speakers.map(s => s.fullName).join(", ")}`
      : "";
    return `${this.session.title}${speakers}`;
  }

  #shareToLinkedIn() {
    const url = encodeURIComponent(this.#getShareUrl());
    const title = encodeURIComponent(this.session?.title ?? "");
    const summary = encodeURIComponent(this.#getShareText());
    window.open(
      `https://www.linkedin.com/shareArticle?mini=true&url=${url}&title=${title}&summary=${summary}`,
      "_blank",
      "noopener,noreferrer"
    );
  }

  #shareToBluesky() {
    const text = encodeURIComponent(`${this.#getShareText()}\n\n${this.#getShareUrl()}`);
    window.open(`https://bsky.app/intent/compose?text=${text}`, "_blank", "noopener,noreferrer");
  }

  #shareToMastodon() {
    const text = encodeURIComponent(`${this.#getShareText()}\n\n${this.#getShareUrl()}`);
    window.open(`https://mastodonshare.com/?text=${text}`, "_blank", "noopener,noreferrer");
  }

  #renderShareSection() {
    return html`
      <div class="share-section">
        <span class="share-label">Share this session:</span>
        <div class="share-buttons">
          <button
            class="share-btn share-btn-linkedin"
            @click=${this.#shareToLinkedIn}
            title="Share on LinkedIn"
            aria-label="Share on LinkedIn"
          >
            ${iconLinkedin}
            <span>LinkedIn</span>
          </button>
          <button
            class="share-btn share-btn-bluesky"
            @click=${this.#shareToBluesky}
            title="Share on Bluesky"
            aria-label="Share on Bluesky"
          >
            ${iconBluesky}
            <span>Bluesky</span>
          </button>
          <button
            class="share-btn share-btn-mastodon"
            @click=${this.#shareToMastodon}
            title="Share on Mastodon"
            aria-label="Share on Mastodon"
          >
            ${iconMastodon}
            <span>Mastodon</span>
          </button>
          <button
            class="share-btn share-btn-copy ${this._showCopiedFeedback ? 'copied' : ''}"
            @click=${this.#shareSession}
            title="${this._showCopiedFeedback ? 'Link copied!' : 'Copy link'}"
            aria-label="${this._showCopiedFeedback ? 'Link copied to clipboard' : 'Copy link to clipboard'}"
          >
            ${this._showCopiedFeedback ? iconCheck : iconCopy}
            <span>${this._showCopiedFeedback ? 'Copied!' : 'Copy link'}</span>
          </button>
        </div>
      </div>
    `;
  }

  renderBody() {
    if (!this.session) {
      return html`<p>No session data available.</p>`;
    }

    const hasDescription = !!this.session.description;
    const hasSidebar = this.session.speakers?.length > 0 ||
                       this.session.startsAt ||
                       this.session.room ||
                       (this.session.categoryItems?.length && this.categories.length);

    // If no description, show a compact single-column layout
    if (!hasDescription) {
      return html`
        <div class="session-dialog-content compact">
          <h2 class="session-title">${this.session.title}</h2>
          ${this.#renderLinks()}
          ${this.#renderSidebar()}
          ${this.#renderShareSection()}
        </div>
      `;
    }

    // Two-column layout with description
    return html`
      <div class="session-dialog-content ${hasSidebar ? 'has-sidebar' : ''}">
        ${this.#renderSidebar()}

        <main class="session-main">
          <h2 class="session-title">${this.session.title}</h2>

          ${this.#renderLinks()}

          <div class="session-description">
            <p>${unsafeHTML(this.#linkifyText(this.session.description ?? ""))}</p>
          </div>

          ${this.#renderShareSection()}
        </main>
      </div>
    `;
  }

  static styles = [
    ...([DcDialogBaseElement.styles].flat()),
    css`
      :host {
        display: block;
        max-width: 850px;
        width: 100%;
        text-align: left;
      }

      .session-dialog-content {
        padding: var(--unit, 1rem);
      }

      .session-dialog-content.has-sidebar {
        display: grid;
        grid-template-columns: 220px 1fr;
        gap: var(--unit-md, 1.5rem);
      }

      /* Compact layout for sessions without description */
      .session-dialog-content.compact {
        max-width: 400px;
      }

      .session-dialog-content.compact .session-sidebar {
        border-right: none;
        padding-right: 0;
      }

      .session-dialog-content.compact .sidebar-section {
        margin-bottom: var(--unit, 1rem);
      }

      .session-dialog-content.compact .speakers-list {
        flex-direction: row;
        flex-wrap: wrap;
      }

      .session-dialog-content.compact .speaker-card {
        flex: 1 1 calc(50% - var(--unit-xs, 0.25rem));
        min-width: 140px;
      }

      .session-main {
        min-width: 0;
      }

      .session-title {
        margin: 0 0 var(--unit, 1rem);
        font-size: 1.5rem;
        color: var(--color-dark, #1b264f);
        text-align: left;
        line-height: 1.3;
      }

      /* Share section */
      .share-section {
        margin-top: var(--unit-md, 1.5rem);
        padding-top: var(--unit, 1rem);
        border-top: 1px solid var(--color-grey-light, #e5e7eb);
      }

      .share-label {
        display: block;
        font-size: 0.85rem;
        font-weight: 600;
        color: var(--color-dark-grey, #6b7280);
        margin-bottom: var(--unit-sm, 0.75rem);
      }

      .share-buttons {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-xs, 0.5rem);
      }

      .share-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.5rem 0.85rem;
        background: var(--color-grey-light, #f5f5f5);
        border: 1px solid var(--color-grey, #d1d5db);
        border-radius: var(--border-radius, 6px);
        color: var(--color-dark, #1b264f);
        font-size: 0.85rem;
        font-weight: 500;
        cursor: pointer;
        transition: all 0.2s ease;
      }

      .share-btn:hover {
        transform: translateY(-1px);
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
      }

      .share-btn .lucide-icon {
        width: 16px;
        height: 16px;
      }

      .share-btn-linkedin:hover {
        background: #0077b5;
        border-color: #0077b5;
        color: var(--color-white, #fff);
      }

      .share-btn-bluesky:hover {
        background: #0085ff;
        border-color: #0085ff;
        color: var(--color-white, #fff);
      }

      .share-btn-mastodon:hover {
        background: #6364ff;
        border-color: #6364ff;
        color: var(--color-white, #fff);
      }

      .share-btn-copy:hover {
        background: var(--color-blue, #3544b1);
        border-color: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
      }

      .share-btn-copy.copied {
        background: var(--color-green, #10b981);
        border-color: var(--color-green, #10b981);
        color: var(--color-white, #fff);
      }

      .session-links {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-sm, 0.75rem);
        margin-bottom: var(--unit, 1rem);
      }

      .session-link {
        display: inline-flex;
        align-items: center;
        padding: var(--unit-xs, 0.5rem) var(--unit, 1rem);
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
        text-decoration: none;
        border-radius: var(--border-radius, 6px);
        font-size: 0.9rem;
        font-weight: 500;
        transition: background-color 0.2s ease;
      }

      .session-link:hover {
        background: var(--color-blue-dark, #2a3690);
      }

      .session-description p {
        margin: 0;
        line-height: 1.7;
        color: var(--color-dark, #1b264f);
        white-space: pre-wrap;
      }

      .session-description a {
        color: var(--color-blue, #3544b1);
        text-decoration: underline;
        word-break: break-word;
      }

      .session-description a:hover {
        color: var(--color-blue-dark, #2a3690);
      }

      /* Sidebar */
      .session-sidebar {
        border-right: 1px solid var(--color-grey-light, #e5e7eb);
        padding-right: var(--unit, 1rem);
      }

      .sidebar-section {
        margin-bottom: var(--unit-md, 1.5rem);
      }

      .sidebar-section:last-child {
        margin-bottom: 0;
      }

      .sidebar-section h3 {
        margin: 0 0 var(--unit-sm, 0.75rem);
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--color-dark-grey, #6b7280);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      /* Speakers in sidebar */
      .speakers-list {
        display: flex;
        flex-direction: column;
        gap: var(--unit-sm, 0.75rem);
      }

      .speaker-card {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: var(--unit-xs, 0.5rem);
        padding: var(--unit-sm, 0.75rem);
        background: var(--color-grey-light, #f5f5f5);
        border: none;
        border-radius: var(--border-radius-xl);
        cursor: pointer;
        transition: background-color 0.2s ease, transform 0.2s ease;
        text-align: center;
      }

      .speaker-card:hover {
        background: var(--color-grey, #e5e7eb);
        transform: translateY(-2px);
      }

      .speaker-photo {
        width: 72px;
        height: 72px;
        border-radius: 50%;
        object-fit: cover;
      }

      .speaker-photo-placeholder {
        display: flex;
        align-items: center;
        justify-content: center;
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
        font-size: 1.5rem;
        font-weight: 600;
        text-transform: uppercase;
      }

      .speaker-card .speaker-name {
        font-size: 0.9rem;
        color: var(--color-dark, #1b264f);
        font-weight: 600;
        line-height: 1.2;
      }

      .speaker-card .speaker-tagline {
        font-size: 0.75rem;
        color: var(--color-dark-grey, #6b7280);
        line-height: 1.3;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
        overflow: hidden;
      }

      /* Meta info in sidebar */
      .sidebar-meta {
        display: flex;
        flex-direction: column;
        gap: var(--unit-xs, 0.5rem);
      }

      .meta-row {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        font-size: 0.9rem;
        color: var(--color-dark, #1b264f);
        line-height: 1.4;
      }

      .meta-icon {
        flex-shrink: 0;
        display: flex;
        align-items: center;
      }

      .meta-icon .lucide-icon {
        width: 18px;
        height: 18px;
      }

      /* Tags in sidebar */
      .sidebar-tags {
        display: flex;
        flex-wrap: wrap;
        justify-content: flex-start;
        gap: var(--unit-xs, 0.5rem);
      }

      .hashtag {
        display: inline-block;
        text-align: left;
        padding: 0.2rem 0.5rem;
        background: transparent;
        border: 1px solid var(--color-blue, #3544b1);
        border-radius: var(--border-radius, 4px);
        color: var(--color-blue, #3544b1);
        font-size: 0.7rem;
        font-weight: 500;
        cursor: pointer;
        transition: background-color 0.2s ease, color 0.2s ease;
        max-width: 100%;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .hashtag:hover {
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
      }

      @media (max-width: 600px) {
        .share-section {
          margin-top: var(--unit, 1rem);
          padding-top: var(--unit-sm, 0.75rem);
        }

        .share-label {
          font-size: 0.8rem;
          margin-bottom: var(--unit-xs, 0.5rem);
        }

        .share-buttons {
          gap: 0.35rem;
        }

        .share-btn {
          padding: 0.4rem 0.6rem;
          font-size: 0.8rem;
          gap: 0.3rem;
        }

        .share-btn .lucide-icon {
          width: 14px;
          height: 14px;
        }

        .session-dialog-content {
          padding: var(--unit-sm, 0.75rem);
        }

        .session-dialog-content.has-sidebar {
          grid-template-columns: 1fr;
          gap: var(--unit-sm, 0.75rem);
        }

        .session-dialog-content.compact {
          max-width: 100%;
        }

        .session-sidebar {
          border-right: none;
          border-bottom: 1px solid var(--color-grey-light, #e5e7eb);
          padding-right: 0;
          padding-bottom: var(--unit-sm, 0.75rem);
          order: -1;
        }

        .sidebar-section {
          margin-bottom: var(--unit-sm, 0.75rem);
        }

        .sidebar-section h3 {
          margin-bottom: var(--unit-xs, 0.5rem);
          font-size: 0.75rem;
        }

        .speakers-list {
          flex-direction: row;
          flex-wrap: wrap;
          gap: var(--unit-xs, 0.5rem);
        }

        .speaker-card {
          flex: 1 1 calc(50% - var(--unit-xs, 0.25rem));
          min-width: 100px;
          padding: var(--unit-xs, 0.5rem);
        }

        .speaker-photo {
          width: 48px;
          height: 48px;
        }

        .speaker-card .speaker-name {
          font-size: 0.8rem;
        }

        .speaker-card .speaker-tagline {
          display: none;
        }

        .session-title {
          font-size: 1.15rem;
          margin-bottom: var(--unit-sm, 0.75rem);
        }

        .session-links {
          margin-bottom: var(--unit-sm, 0.75rem);
        }

        .session-description p {
          font-size: 0.9rem;
          line-height: 1.5;
        }

        .sidebar-meta {
          gap: 0.25rem;
        }

        .meta-row {
          font-size: 0.85rem;
        }

        .sidebar-tags {
          gap: 0.25rem;
        }

        .hashtag {
          padding: 0.15rem 0.4rem;
          font-size: 0.75rem;
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: SessionizeSessionDialogElement;
  }
}
