import { css, html, nothing, TemplateResult } from "lit";
import { customElement, property } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { unsafeHTML } from "lit/directives/unsafe-html.js";
import { DcDialogBaseElement } from "../dialog/dialog-base.element.js";
import { SessionizeSpeaker, SessionizeSchedule } from "../../services/sessionize.service.js";
import {
  iconTwitterX,
  iconLinkedin,
  iconFacebook,
  iconInstagram,
  iconGlobe,
  iconPenLine,
  iconLink,
  iconMic,
  iconCalendar,
  iconClock,
  iconMapPin,
} from "../../svg/lucide-icons.js";

const elementName = "dc-sessionize-speaker-dialog";

@customElement(elementName)
export class SessionizeSpeakerDialogElement extends DcDialogBaseElement {
  @property({ type: Object })
  speaker?: SessionizeSpeaker;

  @property({ type: Array })
  schedule: SessionizeSchedule[] = [];

  @property({ type: String })
  timezone?: string;

  #getLinkIcon(linkType: string): TemplateResult {
    const icons: Record<string, TemplateResult> = {
      Twitter: iconTwitterX,
      LinkedIn: iconLinkedin,
      Blog: iconPenLine,
      Company_Website: iconGlobe,
      Facebook: iconFacebook,
      Instagram: iconInstagram,
      Sessionize: iconMic,
    };
    return icons[linkType] || iconLink;
  }

  #getLinkDisplayText(linkType: string, url: string, title: string): string {
    // Extract Twitter/X handle from URL
    const lowerTitle = title.toLowerCase();
    const lowerUrl = url.toLowerCase();
    const isTwitterLink = lowerTitle.includes("twitter") ||
                          lowerTitle.includes("x (") ||
                          lowerUrl.includes("twitter.com") ||
                          lowerUrl.includes("x.com");

    if (isTwitterLink) {
      try {
        const urlObj = new URL(url);
        // Handle twitter.com/username or x.com/username
        const pathname = urlObj.pathname;
        const handle = pathname.split("/").filter(Boolean)[0];
        if (handle) {
          return `@${handle}`;
        }
      } catch {
        // Fall back to title if URL parsing fails
      }
    }
    return title;
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

  #findSessionDetails(sessionId: number | undefined): { date: string; time: string; room: string; startsAt?: string } | null {
    if (!sessionId || !this.schedule.length) return null;

    for (const day of this.schedule) {
      for (const slot of day.timeSlots) {
        for (const room of slot.rooms) {
          if (room.session && room.session.id === sessionId.toString()) {
            const date = this.#formatDate(day.date);
            const time = this.#formatTime(room.session.startsAt, room.session.endsAt);
            return { date, time, room: room.name, startsAt: room.session.startsAt };
          }
        }
      }
    }
    return null;
  }

  #parseAsUtc(timeString: string): Date {
    let dateTimeStr = timeString;
    if (!dateTimeStr.endsWith("Z") && !dateTimeStr.includes("+") && !dateTimeStr.includes("-", 10)) {
      dateTimeStr += "Z";
    }
    return new Date(dateTimeStr);
  }

  #formatDate(dateString: string): string {
    try {
      const date = new Date(dateString);
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

  #formatTime(startsAt?: string, endsAt?: string): string {
    if (!startsAt) return "";
    try {
      const options: Intl.DateTimeFormatOptions = {
        hour: "2-digit",
        minute: "2-digit",
      };
      if (this.timezone) {
        options.timeZone = this.timezone;
      }
      const start = this.#parseAsUtc(startsAt).toLocaleTimeString("en-GB", options);
      if (endsAt) {
        const end = this.#parseAsUtc(endsAt).toLocaleTimeString("en-GB", options);
        return `${start} - ${end}`;
      }
      return start;
    } catch {
      return "";
    }
  }

  #renderLinks() {
    if (!this.speaker?.links?.length) return nothing;

    return html`
      <div class="speaker-links">
        ${this.speaker.links.map(
          (link) => html`
            <a
              href=${link.url}
              target="_blank"
              rel="noopener noreferrer"
              title=${link.title}
              class="link-icon"
            >
              ${this.#getLinkIcon(link.linkType)}
              <span>${this.#getLinkDisplayText(link.linkType, link.url, link.title)}</span>
            </a>
          `
        )}
      </div>
    `;
  }

  #renderSessions() {
    if (!this.speaker?.sessions?.length) return nothing;

    // Get sessions with their details and sort by start time
    const sessionsWithDetails = this.speaker.sessions
      .map((session) => ({
        session,
        details: this.#findSessionDetails(session.id),
      }))
      .sort((a, b) => {
        // Sessions without start time go to the end
        if (!a.details?.startsAt && !b.details?.startsAt) return 0;
        if (!a.details?.startsAt) return 1;
        if (!b.details?.startsAt) return -1;
        // Sort by start time ascending (earliest first)
        return new Date(a.details.startsAt).getTime() - new Date(b.details.startsAt).getTime();
      });

    return html`
      <div class="speaker-sessions">
        <h3>Sessions</h3>
        <div class="sessions-list">
          ${sessionsWithDetails.map(({ session, details }) => html`
            <div class="session-item">
              <div class="session-name">${session.name}</div>
              ${details
                ? html`
                    <div class="session-details">
                      <span class="session-detail">
                        <span class="detail-icon">${iconCalendar}</span>
                        ${details.date}
                      </span>
                      <span class="session-detail">
                        <span class="detail-icon">${iconClock}</span>
                        ${details.time}
                      </span>
                      <span class="session-detail">
                        <span class="detail-icon">${iconMapPin}</span>
                        ${details.room}
                      </span>
                    </div>
                  `
                : nothing}
            </div>
          `)}
        </div>
      </div>
    `;
  }

  renderBody() {
    if (!this.speaker) {
      return html`<p>No speaker data available.</p>`;
    }

    return html`
      <div class="speaker-dialog-content">
        <div class="speaker-header">
          ${when(
            this.speaker.profilePicture,
            () => html`
              <img
                class="speaker-image"
                src=${this.speaker!.profilePicture!}
                alt=${this.speaker!.fullName}
              />
            `,
            () => html`
              <div class="speaker-image speaker-image-placeholder">
                <span>${this.speaker!.firstName?.[0] ?? ""}${this.speaker!.lastName?.[0] ?? ""}</span>
              </div>
            `
          )}
          <div class="speaker-info">
            <h2 class="speaker-name">${this.speaker.fullName}</h2>
            ${when(
              this.speaker.tagLine,
              () => html`<p class="speaker-tagline">${this.speaker!.tagLine}</p>`
            )}
            ${this.#renderLinks()}
          </div>
        </div>

        ${when(
          this.speaker.bio,
          () => html`
            <div class="speaker-bio">
              <p>${unsafeHTML(this.#linkifyText(this.speaker!.bio!))}</p>
            </div>
          `
        )}

        ${this.#renderSessions()}
      </div>
    `;
  }

  static styles = [
    ...([DcDialogBaseElement.styles].flat()),
    css`
      :host {
        display: block;
        max-width: 600px;
        width: 100%;
      }

      .speaker-dialog-content {
        padding: var(--unit, 1rem);
      }

      .speaker-header {
        display: flex;
        gap: var(--unit-md, 1.5rem);
        align-items: flex-start;
        margin-bottom: var(--unit-md, 1.5rem);
      }

      .speaker-image {
        width: 120px;
        height: 120px;
        border-radius: 50%;
        object-fit: cover;
        flex-shrink: 0;
      }

      .speaker-image-placeholder {
        display: flex;
        align-items: center;
        justify-content: center;
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
        font-size: 2.5rem;
        font-weight: 600;
        text-transform: uppercase;
      }

      .speaker-info {
        flex: 1;
      }

      .speaker-name {
        margin: 0 0 var(--unit-xs, 0.5rem);
        font-size: 1.5rem;
        color: var(--color-dark, #1b264f);
        text-align: left;
      }

      .speaker-tagline {
        margin: 0 0 var(--unit-sm, 0.75rem);
        font-size: 1rem;
        color: var(--color-dark-grey, #6b7280);
      }

      .speaker-links {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-xs, 0.5rem);
      }

      .speaker-links a {
        display: inline-flex;
        align-items: center;
        gap: 0.25rem;
        padding: 0.25rem 0.5rem;
        background: var(--color-grey-light, #f5f5f5);
        border-radius: var(--border-radius, 6px);
        color: var(--color-dark, #1b264f);
        text-decoration: none;
        font-size: 0.875rem;
        transition: background-color 0.2s ease;
      }

      .speaker-links a:hover {
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
      }

      .speaker-links a span {
        max-width: 150px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .speaker-bio {
        margin-bottom: var(--unit-md, 1.5rem);
      }

      .speaker-bio p {
        margin: 0;
        line-height: 1.6;
        color: var(--color-dark, #1b264f);
        white-space: pre-wrap;
      }

      .speaker-bio a {
        color: var(--color-blue, #3544b1);
        text-decoration: underline;
        word-break: break-word;
      }

      .speaker-bio a:hover {
        color: var(--color-blue-dark, #2a3690);
      }

      .speaker-sessions h3 {
        margin: 0 0 var(--unit-sm, 0.75rem);
        font-size: 1.125rem;
        color: var(--color-dark, #1b264f);
      }

      .sessions-list {
        display: flex;
        flex-direction: column;
        gap: var(--unit-sm, 0.75rem);
      }

      .session-item {
        padding: var(--unit-sm, 0.75rem);
        background: var(--color-grey-light, #f5f5f5);
        border-radius: var(--border-radius, 6px);
      }

      .session-name {
        font-weight: 600;
        color: var(--color-dark, #1b264f);
      }

      .session-details {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-sm, 0.75rem);
        margin-top: var(--unit-xs, 0.5rem);
      }

      .session-detail {
        display: inline-flex;
        align-items: center;
        gap: 0.25rem;
        font-size: 0.85rem;
        color: var(--color-dark-grey, #6b7280);
      }

      .detail-icon {
        display: flex;
        align-items: center;
      }

      .detail-icon .lucide-icon {
        width: 16px;
        height: 16px;
      }

      .speaker-links .lucide-icon {
        width: 16px;
        height: 16px;
        flex-shrink: 0;
      }

      @media (max-width: 600px) {
        .speaker-dialog-content {
          padding: var(--unit-sm, 0.75rem);
        }

        .speaker-header {
          flex-direction: column;
          align-items: center;
          text-align: center;
          gap: var(--unit-sm, 0.75rem);
          margin-bottom: var(--unit-sm, 0.75rem);
        }

        .speaker-image {
          width: 80px;
          height: 80px;
        }

        .speaker-image-placeholder {
          font-size: 2rem;
        }

        .speaker-name {
          text-align: center;
          font-size: 1.25rem;
          margin-bottom: var(--unit-xs, 0.5rem);
        }

        .speaker-tagline {
          font-size: 0.9rem;
          margin-bottom: var(--unit-xs, 0.5rem);
        }

        .speaker-links {
          justify-content: center;
          gap: 0.35rem;
        }

        .speaker-links a {
          padding: 0.2rem 0.4rem;
          font-size: 0.8rem;
        }

        .speaker-bio {
          margin-bottom: var(--unit-sm, 0.75rem);
        }

        .speaker-bio p {
          font-size: 0.9rem;
          line-height: 1.5;
        }

        .speaker-sessions h3 {
          font-size: 1rem;
          margin-bottom: var(--unit-xs, 0.5rem);
        }

        .sessions-list {
          gap: var(--unit-xs, 0.5rem);
        }

        .session-item {
          padding: var(--unit-xs, 0.5rem) var(--unit-sm, 0.75rem);
        }

        .session-name {
          font-size: 0.9rem;
        }

        .session-details {
          gap: var(--unit-xs, 0.5rem);
          margin-top: var(--unit-xs, 0.25rem);
        }

        .session-detail {
          font-size: 0.75rem;
        }

        .detail-icon {
          font-size: 0.8rem;
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: SessionizeSpeakerDialogElement;
  }
}
