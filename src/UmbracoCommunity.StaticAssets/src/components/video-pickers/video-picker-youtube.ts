import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';
import { loadYouTubeApi, type YouTubePlayer } from './youtube-api';

const elementName = "dc-video-picker-youtube";

/**
 * Shows a poster and creates the YouTube player only when the visitor presses play, so nothing is requested
 * from YouTube on page load. Browsers often ignore `autoplay=1` in an embed, so playback is started through
 * the player API. Only if the browser then blocks playback with sound does it play muted (YouTube shows its
 * own unmute button).
 */
@customElement(elementName)
export class VideoPickerYouTubeElement extends DcVideoBlockElement {
    protected override readonly posterWithoutThumbnail = true;

    #started = false;
    #player?: YouTubePlayer;

    connectedCallback(): void {
        super.connectedCallback();

        if (this.autoPlay) {
            this.#createIframe(false);
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

        const embed = new URL(this.source, window.location.href);
        const videoId = embed.pathname.split('/').pop();

        let YT;
        try {
            YT = await loadYouTubeApi();
        } catch {
            this.#createIframe(true);
            return;
        }

        // The API replaces the target with its iframe, so the target sits inside a slotted wrapper.
        await this.updateComplete;
        const wrapper = document.createElement('div');
        wrapper.slot = 'video';
        wrapper.style.cssText = 'width:100%;height:100%';
        const target = document.createElement('div');
        wrapper.append(target);
        this.append(wrapper);

        const start = Number(embed.searchParams.get('start')) || undefined;
        this.#player = new YT.Player(target, {
            host: embed.origin,
            videoId,
            width: '100%',
            height: '100%',
            playerVars: { autoplay: 1, playsinline: 1, rel: 0, start },
            events: {
                onReady: ({ target: player }) => {
                    const iframe = player.getIframe();
                    iframe.title = this.name || 'YouTube video';
                    iframe.style.cssText = 'width:100%;height:100%;border:0;display:block';
                    player.playVideo();
                    iframe.focus();
                },
                onAutoplayBlocked: ({ target: player }) => {
                    player.mute();
                    player.playVideo();
                },
            },
        });
    }

    // Used for editor autoplay, and when YouTube's player API can't be loaded.
    #createIframe(startPlaying: boolean) {
        if (!this.source || this.querySelector('iframe')) return;

        const src = new URL(this.source, window.location.href);
        if (startPlaying) {
            src.searchParams.set('autoplay', '1');
            src.searchParams.set('playsinline', '1');
        }

        const iframe = document.createElement('iframe');
        iframe.slot = 'video';
        iframe.allow = this.allow ?? '';
        iframe.src = src.toString();
        iframe.title = this.name || 'YouTube video';
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
