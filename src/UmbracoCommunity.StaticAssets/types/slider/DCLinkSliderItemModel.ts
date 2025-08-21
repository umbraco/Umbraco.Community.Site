import { DcImageModel } from "../DCImageModel";
import { DcLinkModel } from "../DCLinkModel";

export interface DcLinkSliderItemModel {
    title: string;
    description: string;
    image?: DcImageModel;
    link?: DcLinkModel;
}