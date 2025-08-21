import { DcImageModel } from './DCImageModel';
import { DcLinkModel } from './DCLinkModel';

export interface DcIntroTeaserModel {
    text: string;
    image: DcImageModel;
    icon?: DcImageModel;
    bgColor?: string;
    link?: DcLinkModel;
    reversed?: boolean;
}