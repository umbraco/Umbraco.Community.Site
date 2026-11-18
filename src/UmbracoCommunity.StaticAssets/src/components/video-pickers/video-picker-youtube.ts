import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';
import { announcePlay, createYouTubeIframe, playYouTube, stopWhenOtherPlays, type EmbeddedYouTube } from './youtube-player';

const elementName = "dc-video-picker-youtube";

/**
 * Shows a poster and creates the YouTube player only when the visitor presses play, so nothing is requested
 * from YouTube on page load.
 */
@customElement(elementName)
export class VideoPickerYouTubeElement extends DcVideoBlockElement {
    protected override readonly posterWithoutThumbnail = true;

    #started = false;
    #player?: EmbeddedYouTube;
    #wrapper?: HTMLElement;
    #request = 0;
    #stopListening?: () => void;

    connectedCallback(): void {
        super.connectedCallback();
        this.#stopListening = stopWhenOtherPlays(this, () => this.stop());

        if (this.autoPlay && this.source && !this.querySelector('iframe')) {
            const iframe = createYouTubeIframe(this.source, this.#title, this.allow ?? '');
            iframe.slot = 'video';
            this.append(iframe);
        }
    }

    disconnectedCallback(): void {
        super.disconnectedCallback();
        this.#stopListening?.();
        this.stop();
    }

    async playVideo() {
        if (this.#started || !this.source) return;
        this.#started = true;
        this.videoPlayed = true;
        announcePlay(this);
        const request = ++this.#request;

        const wrapper = document.createElement('div');
        wrapper.slot = 'video';
        wrapper.style.cssText = 'width:100%;height:100%';
        this.append(wrapper);
        this.#wrapper = wrapper;

        const player = await playYouTube(wrapper, this.source, this.#title, this.allow ?? '');
        if (request === this.#request && this.isConnected) this.#player = player;
        else player.destroy();
    }

    /** Stops a video started with `playVideo` and shows the poster again. */
    stop() {
        if (!this.#started) return;
        this.#request++;
        this.#player?.destroy();
        this.#player = undefined;
        this.#wrapper?.remove();
        this.#wrapper = undefined;
        this.#started = false;
        this.videoPlayed = false;
    }

    get #title() {
        return this.name || 'YouTube video';
    }
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerYouTubeElement;
    }
}
