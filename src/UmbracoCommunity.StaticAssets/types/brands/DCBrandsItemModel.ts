import { DcImageModel } from "../DCImageModel";
import { DcLinkModel } from "../DCLinkModel";

export interface DcBrandsItemModel {
    title: string;
    image: DcImageModel;
    link: DcLinkModel;
    linkText?: string;
}