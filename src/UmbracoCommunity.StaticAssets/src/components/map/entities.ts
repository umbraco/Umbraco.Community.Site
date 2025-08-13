export interface MapMarkerModel {
    position: {
        lat: number;
        lng: number;
    },
    icon?: string;
    content?: string;
    name: () => string;
};

export interface PartnerMapMarkerModel extends MapMarkerModel {
    partner: {
        name?: string;
        country?: string;
        partnership?: string;
        logo?: string;
        imageBackgroundColor?: string;
        url?: string;
    };   
};
