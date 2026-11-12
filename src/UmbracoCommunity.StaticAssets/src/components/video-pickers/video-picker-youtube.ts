import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';
import { createYouTubeIframe, playYouTube, type EmbeddedYouTube } from './youtube-player';

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

    connectedCallback(): void {
        super.connectedCallback();

        if (this.autoPlay && this.source && !this.querySelector('iframe')) {
            const iframe = createYouTubeIframe(this.source, this.#title, this.allow ?? '');
            iframe.slot = 'video';
            this.append(iframe);
        }
    }

    disconnectedCallback(): void {
        super.disconnectedCallback();
        this.#player?.destroy();
        this.#player = undefined;
    }

    async playVideo() {
        if (this.#started || !this.source) return;
        this.#started = true;
        this.videoPlayed = true;

        const wrapper = document.createElement('div');
        wrapper.slot = 'video';
        wrapper.style.cssText = 'width:100%;height:100%';
        this.append(wrapper);

        const player = await playYouTube(wrapper, this.source, this.#title, this.allow ?? '');
        if (this.isConnected) this.#player = player;
        else player.destroy();
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
