import { customElement } from 'lit/decorators.js';
import { DcVideoBlockElement } from './video-block.element';

const elementName = "dc-video-picker-media";

@customElement(elementName)
export class VideoPickerMediaElement extends DcVideoBlockElement {

    playVideo() {
        const slotElement = document.querySelector('[slot=video]') as HTMLVideoElement;

        this.videoPlayed = true;

        if (slotElement) {
            slotElement.play();
        }
    };
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: VideoPickerMediaElement;
    }
}
