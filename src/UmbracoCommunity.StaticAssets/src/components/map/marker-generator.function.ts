import { PartnerCard } from "../partner";
import { PartnerMapMarkerModel } from "./entities";

export function generatePartnerMarker(
  p: PartnerCard & HTMLElement
): PartnerMapMarkerModel | null {
  // model has been filtered to check coordinates before generating marker
  const coordinates = p.coordinates!.split(",");
  const name = p.querySelector("h3")?.innerText;
  const logo = (p.querySelector("img[slot=logo]") as HTMLImageElement)?.src;

  // Only Platinum and Gold partners should have clickable links
  const isClickablePartner = p.level === "Platinum" || p.level === "Gold";
  const partnerUrl = isClickablePartner ? p.link : undefined;

  // Validate coordinates to prevent NaN values
  const lat = parseFloat(coordinates[0]);
  const lng = parseFloat(coordinates[1]);
  
  if (isNaN(lat) || isNaN(lng)) {
    console.warn(`Skipping partner "${name}" due to invalid coordinates: "${p.coordinates}"`);
    return null;
  }

  return {
    partner: {
      name,
      country: p.country,
      partnership: p.level,
      logo,
      imageBackgroundColor: p.color,
      url: partnerUrl,
    },
    position: {
      lat,
      lng,
    },
    name: () => name ?? "",
    icon: new URL(
      `../../assets/pin-${p.level?.toLowerCase()}.svg`,
      import.meta.url
    ).href,
    content: `
            <dc-partner-map-info-window
                ${name ? `name="${name}"` : ''}
                ${p.country ? `country="${p.country}"` : ''}
                ${p.level ? `partnership="${p.level}"` : ''}
                ${logo ? `logo="${logo}"` : ''}
                ${partnerUrl ? `url="${partnerUrl}"` : ''}>
            </dc-partner-map-info-window>`,
  };
}
