import { css, html, nothing } from "lit";
import { customElement, property } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { DcDialogBaseElement } from "../dialog/dialog-base.element.js";
import { DcDialogHandler } from "../dialog/dialog.handler.js";
import {
  SessionizeSession,
  SessionizeSpeaker,
} from "../../services/sessionize.service.js";
import { SessionizeSpeakerDialogElement } from "./sessionize-speaker-dialog.element.js";

const elementName = "dc-sessionize-session-dialog";

@customElement(elementName)
export class SessionizeSessionDialogElement extends DcDialogBaseElement {
  @property({ type: Object })
  session?: SessionizeSession;

  #dialogHandler = new DcDialogHandler();

  #formatTime(timeString?: string): string {
    if (!timeString) return "";
    try {
      const date = new Date(timeString);
      return date.toLocaleTimeString("en-GB", {
        hour: "2-digit",
        minute: "2-digit",
      });
    } catch {
      return timeString;
    }
  }

  #formatDateRange(): string {
    if (!this.session?.startsAt) return "";
    const start = this.#formatTime(this.session.startsAt);
    const end = this.#formatTime(this.session.endsAt);
    return end ? `${start} - ${end}` : start;
  }

  #openSpeakerDialog(speaker: SessionizeSpeaker, e: Event) {
    e.stopPropagation();
    // Close current dialog first
    this.close();
    // Open speaker dialog
    setTimeout(() => {
      const dialog = new SessionizeSpeakerDialogElement();
      dialog.speaker = speaker;
      this.#dialogHandler.open(dialog);
    }, 100);
  }

  #renderSpeakers() {
    if (!this.session?.speakers?.length) return nothing;

    return html`
      <div class="session-speakers-list">
        <h3>Speakers</h3>
        <div class="speakers-grid">
          ${this.session.speakers.map(
            (speaker) => html`
              <button
                class="speaker-chip"
                @click=${(e: Event) => this.#openSpeakerDialog(speaker, e)}
                title="View ${speaker.fullName}'s profile"
              >
                ${when(
                  speaker.profilePicture,
                  () => html`
                    <img
                      class="speaker-avatar"
                      src=${speaker.profilePicture!}
                      alt=""
                    />
                  `,
                  () => html`
                    <div class="speaker-avatar speaker-avatar-placeholder">
                      ${speaker.firstName?.[0] ?? ""}${speaker.lastName?.[0] ?? ""}
                    </div>
                  `
                )}
                <span class="speaker-name">${speaker.fullName}</span>
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

    return html`
      <div class="session-dialog-content">
        <h2 class="session-title">${this.session.title}</h2>

        <div class="session-meta">
          ${when(
            this.session.startsAt,
            () => html`
              <span class="meta-item time">
                <span class="meta-icon">&#128337;</span>
                ${this.#formatDateRange()}
              </span>
            `
          )}
          ${when(
            this.session.room,
            () => html`
              <span class="meta-item room">
                <span class="meta-icon">&#128205;</span>
                ${this.session!.room}
              </span>
            `
          )}
        </div>

        ${this.#renderLinks()}
        ${this.#renderSpeakers()}

        ${when(
          this.session.description,
          () => html`
            <div class="session-description">
              <h3>About this session</h3>
              <p>${this.session!.description}</p>
            </div>
          `
        )}
      </div>
    `;
  }

  static styles = [
    ...([DcDialogBaseElement.styles].flat()),
    css`
      :host {
        display: block;
        max-width: 700px;
        width: 100%;
      }

      .session-dialog-content {
        padding: var(--unit, 1rem);
      }

      .session-title {
        margin: 0 0 var(--unit, 1rem);
        font-size: 1.5rem;
        color: var(--color-dark, #1b264f);
        text-align: left;
        line-height: 1.3;
      }

      .session-meta {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit, 1rem);
        margin-bottom: var(--unit-md, 1.5rem);
      }

      .meta-item {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        font-size: 0.95rem;
        color: var(--color-grey, #6b7280);
      }

      .meta-icon {
        font-size: 1.1rem;
      }

      .session-links {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-sm, 0.75rem);
        margin-bottom: var(--unit-md, 1.5rem);
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

      .session-speakers-list h3,
      .session-description h3 {
        margin: 0 0 var(--unit-sm, 0.75rem);
        font-size: 1rem;
        font-weight: 600;
        color: var(--color-dark, #1b264f);
        text-align: left;
      }

      .speakers-grid {
        display: flex;
        flex-wrap: wrap;
        gap: var(--unit-sm, 0.75rem);
        margin-bottom: var(--unit-md, 1.5rem);
      }

      .speaker-chip {
        display: inline-flex;
        align-items: center;
        gap: var(--unit-xs, 0.5rem);
        padding: var(--unit-xs, 0.5rem) var(--unit-sm, 0.75rem);
        background: var(--color-grey-light, #f5f5f5);
        border: none;
        border-radius: var(--border-radius-lg, 20px);
        cursor: pointer;
        transition: background-color 0.2s ease, transform 0.2s ease;
      }

      .speaker-chip:hover {
        background: var(--color-grey, #e5e7eb);
        transform: translateY(-1px);
      }

      .speaker-avatar {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        object-fit: cover;
      }

      .speaker-avatar-placeholder {
        display: flex;
        align-items: center;
        justify-content: center;
        background: var(--color-blue, #3544b1);
        color: var(--color-white, #fff);
        font-size: 0.75rem;
        font-weight: 600;
        text-transform: uppercase;
      }

      .speaker-chip .speaker-name {
        font-size: 0.9rem;
        color: var(--color-dark, #1b264f);
        font-weight: 500;
      }

      .session-description {
        margin-top: var(--unit-md, 1.5rem);
      }

      .session-description p {
        margin: 0;
        line-height: 1.6;
        color: var(--color-dark, #1b264f);
        white-space: pre-wrap;
      }

      @media (max-width: 480px) {
        .session-title {
          font-size: 1.25rem;
        }

        .session-meta {
          flex-direction: column;
          gap: var(--unit-xs, 0.5rem);
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
