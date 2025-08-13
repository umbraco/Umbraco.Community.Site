import { DcImageModel } from "./DCImageModel";

export interface DcPageBannerModel {
    text: string;
    bgImage?: DcImageModel;
    bgColor?: string;
}