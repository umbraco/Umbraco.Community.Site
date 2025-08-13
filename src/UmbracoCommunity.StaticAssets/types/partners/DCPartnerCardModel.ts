import { DcImageModel } from "../DCImageModel";

export interface DcPartnerCardModel {
    cardBackgroundColor?: string;
    cardImage: DcImageModel;
    coordinates?: string;
    country: string;
    logo?: DcImageModel;
    name: string;
    partnership: 'Platinum' | 'Gold' | 'Silver' | 'Registered';
    sectors: Array<string>;
    skills: Array<string>;
    url: string;
    website?: string;
}
