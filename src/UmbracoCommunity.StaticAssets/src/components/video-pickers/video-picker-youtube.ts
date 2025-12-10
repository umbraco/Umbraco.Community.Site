import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';
import { YouTubePlayer } from 'youtube-player/dist/types';
import PlayerFactory from 'youtube-player/dist';

const elementName = "dc-video-picker-youtube";

@customElement(elementName)
export class VideoPickerYouTubeElement extends DcVideoBlockElement {
    playVideo() {
        this.videoPlayed = true;

        console.log('play video');
        const slotElement = this.querySelector('[slot=video]') as HTMLElement;
        if (slotElement) {
            var ytPlayer = PlayerFactory(slotElement) as YouTubePlayer;
            if (ytPlayer) {
                ytPlayer.getPlayerState().then((state) => {
                    console.log(state);
                    ytPlayer.playVideo();
                });
            }
        }
    };    
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerYouTubeElement;
    }
}
