import { PartnerCard } from "../partner";
import { PartnerMapMarkerModel } from "./entities";

export function generatePartnerMarker(
  p: PartnerCard & HTMLElement
): PartnerMapMarkerModel {
  // model has been filtered to check coordinates before generating marker
  const coordinates = p.coordinates!.split(",");
  const name = p.querySelector("h3")?.innerText;
  const logo = (p.querySelector("img[slot=logo]") as HTMLImageElement)?.src;

  return {
    partner: {
      name,
      country: p.country,
      partnership: p.level,
      logo,
      imageBackgroundColor: p.color,
      url: p.link,
    },
    position: {
      lat: parseFloat(coordinates[0]),
      lng: parseFloat(coordinates[1]),
    },
    name: () => name ?? "",
    icon: new URL(
      `../../assets/pin-${p.level?.toLowerCase()}.svg`,
      import.meta.url
    ).href,
    content: `
            <dc-partner-map-info-window
                name="${name}"
                country="${p.country}"
                partnership="${p.level}"
                logo="${logo}"
                url="${p.link}">
            </dc-partner-map-info-window>`,
  };
}
