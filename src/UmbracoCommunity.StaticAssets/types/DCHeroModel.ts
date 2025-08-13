import { DcImageModel } from "./DCImageModel";
import { DcLinkModel } from "./DCLinkModel";

export interface DcHeroModel {
    text: string;
    bgImage?: DcImageModel;
    links?: Array<DcLinkModel>;
}