import { DcImageModel } from "./DCImageModel";

export interface DcImageBlockModel {
    fullWidthImage?: DcImageModel;
    halfWidthImage?: DcImageModel;
    widthMode: 'half' | 'full';
    text?: string;
    overlay?: boolean;
}