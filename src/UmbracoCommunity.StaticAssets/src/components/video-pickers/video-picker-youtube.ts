import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';
import { YouTubePlayer } from 'youtube-player/dist/types';
import PlayerFactory from 'youtube-player/dist';

declare const YT: any;
let player: any;

const elementName = "dc-video-picker-youtube";

@customElement(elementName)
export class VideoPickerYouTubeElement extends DcVideoBlockElement {
    connectedCallback(): void {
        super.connectedCallback();

        if (typeof(YT) == 'undefined' || typeof(YT.Player) == 'undefined') {
            var tag = document.createElement('script');
            tag.src = "https://www.youtube.com/iframe_api";
            var firstScriptTag = document.getElementsByTagName('script')[0];
            if (firstScriptTag && firstScriptTag.parentNode) {
                firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
            }
        }
    }
    
    playVideo() {
        this.videoPlayed = true;

        player = new YT.Player('player', {
            events: {
                'onReady': this.onPlayerReady
            }
        });
    };

    onPlayerReady(event: any) {
        player.playVideo();
    }
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerYouTubeElement;
    }
}
