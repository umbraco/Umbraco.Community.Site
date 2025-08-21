import { DcImageModel } from "../DCImageModel";

export interface DcMapMarkerModel {
    partner: {
        name: string;
        country: string;
        partnership: string;
        image?: DcImageModel;
        imageBackgroundColor?: string;
        url?: string;
    };
    position: {
        lat: number;
        lng: number;
    },
    icon?: string;
    content?: string;
    name: () => string;
}
