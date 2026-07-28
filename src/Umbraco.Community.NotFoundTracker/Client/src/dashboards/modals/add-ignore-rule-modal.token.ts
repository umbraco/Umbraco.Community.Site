import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface AddIgnoreRuleModalData {
  /** Hit id when triggered from the hits list (0 = standalone "Add rule" flow). */
  hitId: number;
  /** Pre-filled path — the hit's path, or empty for standalone. */
  suggestedPath: string;
  /** Pre-filled hostname — the hit's hostname, or null for standalone. */
  suggestedHostname: string | null;
}

export type AddIgnoreRuleModalValue = undefined;

export const ADD_IGNORE_RULE_MODAL = new UmbModalToken<AddIgnoreRuleModalData, AddIgnoreRuleModalValue>(
  "NotFoundTracker.Modal.AddIgnoreRule",
  {
    modal: {
      type: "sidebar",
      size: "small",
    },
  },
);
