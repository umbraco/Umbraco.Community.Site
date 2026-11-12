import { loadYouTubeApi, type YouTubePlayer } from './youtube-api';

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

const UNMUTE_CHECK_MS = 1000;

// Shown when the browser only allows muted playback; a click on the page is allowed to turn the sound on.
function showUnmuteButton(container: HTMLElement, player: YouTubePlayer): () => void {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'dc-youtube-unmute';
    button.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M11 5 6 9H2v6h4l5 4V5Z"/><path d="M15.5 8.5a5 5 0 0 1 0 7M19 5a10 10 0 0 1 0 14" fill="none"/></svg><span>Turn sound on</span>';

    const remove = () => {
        window.clearInterval(watch);
        button.remove();
    };
    // Also goes if the visitor unmutes with YouTube's own control.
    const watch = window.setInterval(() => {
        if (!player.isMuted()) remove();
    }, UNMUTE_CHECK_MS);

    button.addEventListener('click', () => {
        player.unMute();
        player.playVideo();
        remove();
        player.getIframe().focus();
    });

    if (getComputedStyle(container).position === 'static') container.style.position = 'relative';
    container.append(button);
    button.focus();
    return remove;
}

/**
 * Plays a YouTube embed URL inside `container`. Browsers often ignore `autoplay=1` in an embed, so playback
 * is started through the player API. If the browser then blocks playback with sound, it plays muted and
 * offers a "Turn sound on" button. Falls back to a plain autoplaying iframe if the API can't load.
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
    let removeUnmute: (() => void) | undefined;
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
                if (!removeUnmute) iframe.focus();
            },
            onAutoplayBlocked: ({ target: blocked }) => {
                blocked.mute();
                blocked.playVideo();
                removeUnmute ??= showUnmuteButton(container, blocked);
            },
        },
    });

    return {
        destroy: () => {
            removeUnmute?.();
            player.destroy();
        },
    };
}
