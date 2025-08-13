import { PartnershipLevels } from "./partnership-levels.enum.js";

export function getPartnershipColor(partnership?: string): string {

    if (partnership === PartnershipLevels.Platinum) {
        return '#6E88AD';
    }

    if (partnership === PartnershipLevels.Gold) {
        return '#CA9B2C';
    }

    if (partnership === PartnershipLevels.Silver) {
        return '#7F8386';
    }

    // KnownPartnershipLevels.Registered
    return '#BF7441';
}
