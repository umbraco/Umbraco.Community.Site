/** The parts of YouTube's IFrame Player API that the video picker uses. */
export interface YouTubePlayer {
    playVideo(): void;
    mute(): void;
    unMute(): void;
    isMuted(): boolean;
    getIframe(): HTMLIFrameElement;
    destroy(): void;
}

export interface YouTubeNamespace {
    Player: new (
        element: HTMLElement,
        options: {
            host?: string;
            videoId?: string;
            width?: string;
            height?: string;
            playerVars?: Record<string, number | string | undefined>;
            events?: {
                onReady?: (event: { target: YouTubePlayer }) => void;
                /** Fires when the browser refuses playback, e.g. with sound before the visitor has interacted enough. */
                onAutoplayBlocked?: (event: { target: YouTubePlayer }) => void;
            };
        },
    ) => YouTubePlayer;
}

type YouTubeWindow = Window & { YT?: YouTubeNamespace; onYouTubeIframeAPIReady?: () => void };

const API_URL = 'https://www.youtube.com/iframe_api';

let loading: Promise<YouTubeNamespace> | undefined;

/** Loads YouTube's IFrame Player API once per page. Only call it after the visitor has asked to play a video. */
export function loadYouTubeApi(): Promise<YouTubeNamespace> {
    const w = window as YouTubeWindow;
    if (w.YT?.Player) return Promise.resolve(w.YT);

    loading ??= new Promise<YouTubeNamespace>((resolve, reject) => {
        const previous = w.onYouTubeIframeAPIReady;
        w.onYouTubeIframeAPIReady = () => {
            previous?.();
            resolve(w.YT!);
        };

        const script = document.createElement('script');
        script.src = API_URL;
        script.async = true;
        script.onerror = () => {
            loading = undefined;
            reject(new Error('YouTube player API failed to load'));
        };
        document.head.append(script);
    });

    return loading;
}

/** For tests: forget a previous load. */
export function resetYouTubeApiForTests() {
    loading = undefined;
}
