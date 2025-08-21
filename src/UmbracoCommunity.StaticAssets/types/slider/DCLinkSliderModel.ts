import { DcLinkSliderItemModel } from "./DCLinkSliderItemModel";

export interface DcLinkSliderModel {
    title?: string;
    description?: string;
    items: Array<DcLinkSliderItemModel>;
}