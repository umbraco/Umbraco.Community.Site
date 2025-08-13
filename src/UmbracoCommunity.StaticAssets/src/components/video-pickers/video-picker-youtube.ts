import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';

const elementName = "dc-video-picker-youtube";

@customElement(elementName)
export class VideoPickerYouTubeElement extends DcVideoBlockElement {
    playVideo() {
        this.videoPlayed = true;
    };    
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerYouTubeElement;
    }
}
