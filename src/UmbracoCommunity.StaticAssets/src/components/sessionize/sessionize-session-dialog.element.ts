import { css, html, nothing } from "lit";
import { customElement, property } from "lit/decorators.js";
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

  #dialogHandler = new DcDialogHandler();

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
                    <span class="meta-icon">&#128337;</span>
                    <span>${this.#formatDateRange()}</span>
                  </div>
                `
              )}
              ${when(
                this.session?.room,
                () => html`
                  <div class="meta-row">
                    <span class="meta-icon">&#128205;</span>
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
        font-size: 1rem;
      }

      /* Tags in sidebar */
      .sidebar-tags {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-xs, 0.5rem);
      }

      .hashtag {
        display: inline-block;
        padding: 0.2rem 0.5rem;
        background: transparent;
        border: 1px solid var(--color-blue, #3544b1);
        border-radius: var(--border-radius, 4px);
        color: var(--color-blue, #3544b1);
        font-size: 0.8rem;
        font-weight: 500;
        cursor: pointer;
        transition: background-color 0.2s ease, color 0.2s ease;
      }

      .hashtag:hover {
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
      }

      @media (max-width: 600px) {
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
