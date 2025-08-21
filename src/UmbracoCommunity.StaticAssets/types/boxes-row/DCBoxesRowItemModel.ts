import { DcImageModel } from "../DCImageModel";
import { DcLinkModel } from "../DCLinkModel";

export interface DcBoxesRowItemModel {
    title: string;
    description: string;
    fontColor: string;
    icon?: DcImageModel;
    link?: DcLinkModel;
}