import { css, html, nothing } from "lit";
import { customElement, property } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { DcDialogBaseElement } from "../dialog/dialog-base.element.js";
import { SessionizeSpeaker } from "../../services/sessionize.service.js";

const elementName = "dc-sessionize-speaker-dialog";

@customElement(elementName)
export class SessionizeSpeakerDialogElement extends DcDialogBaseElement {
  @property({ type: Object })
  speaker?: SessionizeSpeaker;

  #getLinkIcon(linkType: string): string {
    const icons: Record<string, string> = {
      Twitter: "𝕏",
      LinkedIn: "in",
      Blog: "📝",
      Company_Website: "🌐",
      Facebook: "f",
      Instagram: "📷",
      Sessionize: "S",
    };
    return icons[linkType] || "🔗";
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
              <span>${link.title}</span>
            </a>
          `
        )}
      </div>
    `;
  }

  #renderSessions() {
    if (!this.speaker?.sessions?.length) return nothing;

    return html`
      <div class="speaker-sessions">
        <h3>Sessions</h3>
        <ul>
          ${this.speaker.sessions.map(
            (session) => html`<li>${session.name}</li>`
          )}
        </ul>
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
              <p>${this.speaker!.bio}</p>
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
        color: var(--color-grey, #6b7280);
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

      .speaker-sessions h3 {
        margin: 0 0 var(--unit-sm, 0.75rem);
        font-size: 1.125rem;
        color: var(--color-dark, #1b264f);
      }

      .speaker-sessions ul {
        margin: 0;
        padding-left: 1.25rem;
      }

      .speaker-sessions li {
        margin-bottom: var(--unit-xs, 0.5rem);
        color: var(--color-dark, #1b264f);
      }

      @media (max-width: 480px) {
        .speaker-header {
          flex-direction: column;
          align-items: center;
          text-align: center;
        }

        .speaker-name {
          text-align: center;
        }

        .speaker-links {
          justify-content: center;
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
