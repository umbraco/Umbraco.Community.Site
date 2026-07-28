import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface HitDetailsModalData {
  hitId: number;
}

export type HitDetailsModalValue = undefined;

export const HIT_DETAILS_MODAL = new UmbModalToken<HitDetailsModalData, HitDetailsModalValue>(
  "NotFoundTracker.Modal.HitDetails",
  {
    modal: {
      type: "sidebar",
      size: "medium",
    },
  },
);
