import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';

const elementName = "dc-video-picker-media";

@customElement(elementName)
export class VideoPickerMediaElement extends DcVideoBlockElement {

    connectedCallback(): void {
        super.connectedCallback();

        // Only auto-play if autoPlay attribute is set
        if (this.autoPlay) {
            // Use setTimeout to ensure video element is fully initialized
            setTimeout(() => this.handleAutoplay(), 100);
        }
    }

    private handleAutoplay(): void {
        const slotElement = this.querySelector('[slot=video]') as HTMLVideoElement;
        if (!slotElement) return;

        // Browsers require videos to be muted for autoplay to work
        // The video element should already have muted="true" from the Razor view
        // This ensures autoplay works reliably across all browsers
        const playPromise = slotElement.play();

        if (playPromise !== undefined) {
            playPromise.catch(error => {
                // Autoplay was prevented by browser 
                // This is normal if user hasn't interacted with the page
                console.warn('Autoplay prevented:', error);
            });
        }

        this.videoPlayed = true;
    }

    playVideo() {
        const slotElement = this.querySelector('[slot=video]') as HTMLVideoElement;

        if (!slotElement) return;

        // Lazy load the video source when user clicks play
        // This prevents bandwidth consumption until the user actually wants to watch
        const source = slotElement.querySelector('source') as HTMLSourceElement;

        if (source && !source.src && this.source) {
            source.src = this.source;
            slotElement.load();
        }

        this.videoPlayed = true;

        // When user clicks play button, unmute the video for better UX
        slotElement.muted = false;

        slotElement.play().catch(error => {
            console.error('Video play failed:', error);
        });
    };
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerMediaElement;
    }
}
