import { loadYouTubeApi } from './youtube-api';

export interface EmbeddedYouTube {
    destroy(): void;
}

const IFRAME_STYLE = 'width:100%;height:100%;border:0;display:block';

/** A plain YouTube iframe for an embed URL, optionally asking it to start playing. */
export function createYouTubeIframe(source: string, title: string, allow = '', startPlaying = false): HTMLIFrameElement {
    const src = new URL(source, window.location.href);
    if (startPlaying) {
        src.searchParams.set('autoplay', '1');
        src.searchParams.set('playsinline', '1');
    }

    const iframe = document.createElement('iframe');
    iframe.allow = allow;
    iframe.src = src.toString();
    iframe.title = title;
    iframe.allowFullscreen = true;
    iframe.setAttribute('frameborder', '0');
    iframe.style.cssText = IFRAME_STYLE;
    return iframe;
}

/**
 * Plays a YouTube embed URL inside `container`. Browsers often ignore
 * `autoplay=1` in an embed, so playback is started through the player API; only if the browser then blocks
 * playback with sound does it play muted. Falls back to a plain autoplaying iframe if the API can't load.
 */
export async function playYouTube(container: HTMLElement, source: string, title: string, allow = ''): Promise<EmbeddedYouTube> {
    const embed = new URL(source, window.location.href);

    let YT;
    try {
        YT = await loadYouTubeApi();
    } catch {
        const iframe = createYouTubeIframe(source, title, allow, true);
        container.append(iframe);
        iframe.focus();
        return { destroy: () => iframe.remove() };
    }

    // The API replaces the target with its iframe.
    const target = document.createElement('div');
    container.append(target);

    const start = Number(embed.searchParams.get('start')) || undefined;
    const player = new YT.Player(target, {
        host: embed.origin,
        videoId: embed.pathname.split('/').pop(),
        width: '100%',
        height: '100%',
        playerVars: { autoplay: 1, playsinline: 1, rel: 0, start },
        events: {
            onReady: ({ target: ready }) => {
                const iframe = ready.getIframe();
                iframe.title = title;
                iframe.style.cssText = IFRAME_STYLE;
                ready.playVideo();
                iframe.focus();
            },
            onAutoplayBlocked: ({ target: blocked }) => {
                blocked.mute();
                blocked.playVideo();
            },
        },
    });

    return { destroy: () => player.destroy() };
}
