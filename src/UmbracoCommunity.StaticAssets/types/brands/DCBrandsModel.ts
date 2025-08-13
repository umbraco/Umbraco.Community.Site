import { DcLinkModel } from "../DCLinkModel";
import { DcBrandsItemModel } from "./DCBrandsItemModel";

export interface DcBrandsModel {
    description?: string;
    items: Array<DcBrandsItemModel>;
    link: DcLinkModel;
}