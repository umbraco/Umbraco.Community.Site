import { DcImageModel } from "../DCImageModel";
import { DcLinkModel } from "../DCLinkModel";

export interface DcBoxSliderItemModel {
    headline: string;
    text: string;
    image: DcImageModel;
    bgColor: string;
    style: string;
    link?: DcLinkModel;
}