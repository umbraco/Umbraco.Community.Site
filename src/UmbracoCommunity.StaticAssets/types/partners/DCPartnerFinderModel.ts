import { DcPartnerCardModel } from "./DCPartnerCardModel";

export interface DcPartnerFinderModel {
    header?: string;
    description?: string;
    skills: Array<Option>;
    partners: Array<DcPartnerCardModel>;
    countries: Array<Option>;
    sectors: Array<Option>;
}

export type DcPartnerFinderRenderMode = 'map' | 'grid';