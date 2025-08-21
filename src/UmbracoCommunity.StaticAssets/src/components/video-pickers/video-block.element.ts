import { css, html, LitElement } from "lit";
import { property, state } from "lit/decorators.js";

export abstract class DcVideoBlockElement extends LitElement {
    @property()
    source!: string;

    @property()
    allow!: string;

    @property()
    format!: string;

    @property()
    name?: string;

    @property()
    length?: string;

    @property()
    thumbnail?: string;

    @state()
    videoPlayed = false;

    abstract playVideo()

    #renderPlayBtn() {
        return html`
        <svg class="play-icon" id="Layer_1" data-name="Layer 1" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><defs><style>.cls-1{fill:#fff}</style></defs><path class="cls-1" d="M49.31 98.42A47.69 47.69 0 1 1 97 50.74a47.74 47.74 0 0 1-47.69 47.68Zm0-90.3a42.62 42.62 0 1 0 42.62 42.62A42.67 42.67 0 0 0 49.31 8.12Z"/><path class="cls-1" d="M35.25 73.01V26.28l40.47 23.37-40.47 23.36z"/></svg>`;
    }

    #renderPoster() {
        const hasDetails = this.name && this.name?.length > 0 ? true : false;

        return html`
            <div class="poster ${!hasDetails ? 'no-details' : ''}" style="background-image: url('${this.thumbnail}');" @click=${this.playVideo}>
                <div class="details">
                    ${this.#renderPlayBtn()}
                    ${hasDetails ? html`<p>${this.name}<span>${this.length}</span></p>` : ''}
                </div>
            </div>
        `;
    }

    render() {
        return html`
            <style>
                slot, :host {
                    aspect-ratio: ${this.format === '169' ? '16 / 9' : '4 / 3'};
                }
            </style>
            <div class="video-picker">
                ${this.videoPlayed || (!this.thumbnail || this.thumbnail.length === 0)
                ? html`<slot name="video"></slot>`
                : this.#renderPoster()
            }
            </div>
        `;
    }

    static styles = [
        css`
            :host {
                position: relative;
            }

            :host,
            slot {
                display:block;
            }

            .poster {
                display: flex;
                position: absolute;
                top: 0;
                height: 100%;
                width: 100%;
                background-size: cover;
                background-position: center;
                cursor: pointer;
            }

            .details {
                position: absolute;
                display: flex;
                bottom: 0;
                right: 0;
                left: 0;
                background-color: rgba(0, 0, 0, 0.49);
                padding: 10px;
                color: #fff;
            }

            .details p {
                display: flex;
                flex-direction: column;
                margin: 0 0 0 10px;
                font-weight: bold;
                font-size: 18px;
                justify-content: center;
            }

            .details p span {
                font-weight: normal;
                font-size: 14px;
            }

            .play-icon {
                align-self: flex-start;
                width: 48px;
                cursor: pointer;
            }

            .poster.no-details .play-icon {
                margin: auto;
            }

            .poster.no-details .details {
                top: 0;
                padding: 0;
                background-color: rgba(0, 0, 0, 0.1);
            }
    `,
    ];
}