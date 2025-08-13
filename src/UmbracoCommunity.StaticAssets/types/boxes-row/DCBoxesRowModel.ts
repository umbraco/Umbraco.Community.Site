import { DcBoxesRowItemModel } from "./DCBoxesRowItemModel";

export interface DcBoxesRowModel {
    text?: string;
    textColor?: string;
    items: Array<DcBoxesRowItemModel>;
}