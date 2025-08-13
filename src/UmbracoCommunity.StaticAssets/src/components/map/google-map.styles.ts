const waterColor = '#192330';
const landColor = '#2A456A';
const roadColor = '#3d5476';

export const GoogleMapStyles: Array<google.maps.MapTypeStyle> = [
    {
        featureType: 'water',
        elementType: 'geometry',
        stylers: [{ color: waterColor }]
    },
    {
        featureType: 'landscape',
        elementType: 'geometry',
        stylers: [{ color: landColor }]
    },
    {
        featureType: 'road.highway',
        elementType: 'geometry.fill',
        stylers: [
            {
                color: roadColor
            }
        ]
    },
    {
        featureType: 'road.highway',
        elementType: 'geometry.stroke',
        stylers: [
            {
                visibility: 'off'
            }
        ]
    },
    {
        featureType: 'road.arterial',
        elementType: 'geometry',
        stylers: [
            {
                color: roadColor
            }
        ]
    },
    {
        featureType: 'road.local',
        elementType: 'geometry',
        stylers: [
            {
                color: roadColor
            }
        ]
    },
    {
        featureType: 'poi',
        elementType: 'geometry',
        stylers: [{ visibility: 'off' }]
    },
    {
        featureType: 'poi.park',
        elementType: 'geometry',
        stylers: [{ visibility: 'off' }]
    },
    {
        featureType: 'all',
        elementType: 'labels.text.fill',
        stylers: [
            {
                color: '#ffffff'
            }
        ]
    },
    {
        featureType: 'all',
        elementType: 'labels.text.stroke',
        stylers: [
            {
                color: '#000000'
            },
            {
                lightness: 13
            }
        ]
    },
    { elementType: 'labels.icon', stylers: [{ visibility: 'off' }] },
    {
        featureType: 'transit',
        elementType: 'geometry',
        stylers: [{ visibility: 'off' }]
    },
    {
        featureType: 'administrative',
        elementType: 'geometry.fill',
        stylers: [{ visibility: 'off' }]
    },
    {
        featureType: 'administrative',
        elementType: 'geometry.stroke',
        stylers: [{ visibility: 'off' }]
    }
];
