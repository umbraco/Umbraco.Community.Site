import { LitElement, css, html, nothing } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import {
  SessionizeService,
  SessionizeSpeaker,
} from "../../services/sessionize.service.js";
import { DcDialogHandler } from "../dialog/dialog.handler.js";
import { SessionizeSpeakerDialogElement } from "./sessionize-speaker-dialog.element.js";

const elementName = "dc-sessionize-speakers";

@customElement(elementName)
export class SessionizeSpeakersElement extends LitElement {
  /**
   * Optional: Filter to only show top speakers
   */
  @property({ type: Boolean, attribute: "top-speakers-only" })
  topSpeakersOnly = false;

  /**
   * Optional: Maximum number of speakers to display
   */
  @property({ type: Number, attribute: "max-speakers" })
  maxSpeakers?: number;

  /**
   * Optional: Number of columns on desktop (default: 4, max: 5)
   */
  @property({ type: Number, attribute: "columns" })
  columns = 4;

  /**
   * Optional: URL of the program page, used to link session titles
   */
  @property({ type: String, attribute: "program-url" })
  programUrl?: string;

  @state()
  private _speakers: SessionizeSpeaker[] = [];

  @state()
  private _loading = true;

  @state()
  private _error: string | null = null;

  @state()
  private _statusMessage = "";

  #dialogHandler = new DcDialogHandler();

  connectedCallback() {
    super.connectedCallback();
    this.#loadSpeakers();
  }

  async #loadSpeakers() {
    try {
      this._loading = true;
      this._error = null;
      this._statusMessage = "Loading speakers...";

      let speakers = await SessionizeService.getSpeakers();

      // Filter top speakers if requested
      if (this.topSpeakersOnly) {
        speakers = speakers.filter((s) => s.isTopSpeaker);
      }

      // Limit the number of speakers if maxSpeakers is set
      if (this.maxSpeakers && this.maxSpeakers > 0) {
        speakers = speakers.slice(0, this.maxSpeakers);
      }

      this._speakers = speakers;

      // Announce results to screen readers
      const count = this._speakers.length;
      this._statusMessage = count === 0
        ? "No speakers found"
        : `Loaded ${count} speaker${count !== 1 ? "s" : ""}`;
    } catch (error) {
      this._error =
        error instanceof Error ? error.message : "Failed to load speakers";
      this._statusMessage = `Error: ${this._error}`;
      console.error("Error loading speakers:", error);
    } finally {
      this._loading = false;
    }
  }

  #renderLoading() {
    return html`
      <div class="loading">
        <div class="spinner"></div>
        <p>Loading speakers...</p>
      </div>
    `;
  }

  #renderError() {
    return html`
      <div class="error">
        <p>${this._error}</p>
        <button @click=${this.#loadSpeakers}>Try again</button>
      </div>
    `;
  }

  #openSpeakerDialog(speaker: SessionizeSpeaker) {
    const dialog = new SessionizeSpeakerDialogElement();
    dialog.speaker = speaker;
    dialog.programUrl = this.programUrl;
    this.#dialogHandler.open(dialog);
  }

  #renderSpeaker(speaker: SessionizeSpeaker) {
    return html`
      <div
        class="dc-speakers-grid__item"
        @click=${() => this.#openSpeakerDialog(speaker)}
        aria-label="View ${speaker.fullName}'s profile"
      >
        ${when(
          speaker.profilePicture,
          () => html`
            <img
                class="dc-image is-circle"
                src=${speaker.profilePicture!}
                alt=${speaker.fullName}
                loading="lazy"
              />
          `,
          () => html`
            <div class="speaker-image speaker-image-placeholder">
              <span>${speaker.firstName?.[0] ?? ""}${speaker.lastName?.[0] ?? ""}</span>
            </div>
          `
        )}
        <p class="speaker-name">${speaker.fullName}</p>
        ${when(
            speaker.pronouns,
            () => html`<p class="speaker-pronouns">${speaker.pronouns}</p>`
          )}
        ${when(
            speaker.tagLine,
            () => html`<p class="speaker-tagline">${speaker.tagLine}</p>`
          )}
      </div>
    `;
  }

  #renderSpeakers() {
    if (!this._speakers.length) {
      return html`<div class="dc-speakers-grid empty">No speakers found.</div>`;
    }

    const cols = Math.min(Math.max(this.columns, 1), 5);
    return html`
      <div class="dc-speakers-grid" data-columns="${cols}">
        ${this._speakers.map((speaker) => this.#renderSpeaker(speaker))}
      </div>
    `;
  }

  #renderStatusAnnouncer() {
    return html`
      <div
        class="sr-only"
        role="status"
        aria-live="polite"
        aria-atomic="true"
      >${this._statusMessage}</div>
    `;
  }

  render() {
    return html`
      ${this.#renderStatusAnnouncer()}
      ${this._loading
        ? this.#renderLoading()
        : this._error
          ? this.#renderError()
          : this.#renderSpeakers()}
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    /* Screen reader only - visually hidden but accessible */
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .dc-speakers-grid {
        display: grid;
        justify-items: center;
        margin-top: var(--unit-lg);
        grid-template-columns: 1fr;
        gap: var(--unit-md);
    }
    
    @media (min-width: 768px) {
        .dc-speakers-grid {
            grid-template-columns: repeat(2, 1fr);
        }
    }

    @media (min-width: 1024px) {
        .dc-speakers-grid {
            grid-template-columns: repeat(4, 1fr);
            gap: var(--unit-md) var(--unit-lg);
        }

        .dc-speakers-grid[data-columns="3"] {
            grid-template-columns: repeat(3, 1fr);
        }

        .dc-speakers-grid[data-columns="5"] {
            grid-template-columns: repeat(5, 1fr);
        }
    }

    .dc-speakers-grid__item {
        display: flex;
        flex-direction: column;
        align-items: center;
        background: transparent;
        border: none;
        padding: var(--unit, 1rem);
        cursor: pointer;
        border-radius: var(--border-radius-xl, 12px);
        transition: background-color 0.2s ease, transform 0.2s ease;

        .dc-image {
            width: 150px;
            object-fit: cover;
            height: 150px;
        }

        .speaker-name {
            font-weight: bold;
            font-size: var(--font-size-large);
            margin: 1rem 0 0;
            text-align: center;
        }

        .speaker-pronouns {
            font-size: var(--font-size-sm);
            color: var(--color-dark-grey, #6b7280);
            margin: 0.25rem 0 0;
            text-align: center;
        }

        .speaker-tagline {
            font-size: var(--font-size-sm);
            margin: 0;
            text-align: center;
        }
    }

    .dc-speakers-grid__item:hover {
        background: var(--color-grey-light, #f5f5f5);
        transform: translateY(-2px);
    }

    .dc-speakers-grid__item:focus {
        outline: 2px solid var(--color-blue, #3544b1);
        outline-offset: 2px;
    }

    .loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-grey, #6b7280);
    }

    .spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-grey-light, #e5e7eb);
      border-top-color: var(--color-blue, #3544b1);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    img.is-circle {
      clip-path: circle();
    }

    .loading p {
      margin-top: var(--unit, 1rem);
    }

    .error {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-red, #dc2626);
      text-align: center;
    }

    .error button {
      margin-top: var(--unit, 1rem);
      padding: var(--unit-xs, 0.5rem) var(--unit, 1rem);
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
      border: none;
      border-radius: var(--border-radius, 6px);
      cursor: pointer;
      font-size: 0.875rem;
      transition: background-color 0.2s ease;
    }

    .error button:hover {
      background: var(--color-blue-dark, #2a3690);
    }

    .dc-speakers-grid.empty {
      text-align: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-grey, #6b7280);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: SessionizeSpeakersElement;
  }
}
