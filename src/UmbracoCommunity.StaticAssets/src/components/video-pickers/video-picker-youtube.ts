import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';

const elementName = "dc-video-picker-youtube";

/**
 * Shows a poster and only creates the YouTube iframe when the visitor presses play (or when the editor
 * turned on autoplay), so nothing is requested from YouTube on page load.
 */
@customElement(elementName)
export class VideoPickerYouTubeElement extends DcVideoBlockElement {
    protected override readonly posterWithoutThumbnail = true;

    connectedCallback(): void {
        super.connectedCallback();

        if (this.autoPlay) {
            this.#createIframe(false);
        }
    }

    playVideo() {
        this.videoPlayed = true;
        this.#createIframe(true);
    }

    #createIframe(startPlaying: boolean) {
        if (!this.source || this.querySelector('iframe')) return;

        const src = new URL(this.source, window.location.href);
        if (startPlaying) src.searchParams.set('autoplay', '1');

        const iframe = document.createElement('iframe');
        iframe.slot = 'video';
        iframe.src = src.toString();
        iframe.title = this.name || 'YouTube video';
        iframe.allow = this.allow ?? '';
        iframe.allowFullscreen = true;
        iframe.setAttribute('frameborder', '0');
        iframe.style.cssText = 'width:100%;height:100%;border:0;display:block';
        this.append(iframe);

        if (startPlaying) {
            this.updateComplete.then(() => iframe.focus());
        }
    }
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerYouTubeElement;
    }
}
