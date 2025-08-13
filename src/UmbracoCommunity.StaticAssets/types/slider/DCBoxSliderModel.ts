import { DcBoxSliderItemModel } from "./DCBoxSliderItemModel";

export interface DcBoxSliderModel {
    title?: string;
    description?: string;
    items: Array<DcBoxSliderItemModel>;
}