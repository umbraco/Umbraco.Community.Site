import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface PostDetailsModalData {
  sphereId: string;
}

export type PostDetailsModalValue = undefined;

export const POST_DETAILS_MODAL = new UmbModalToken<PostDetailsModalData, PostDetailsModalValue>(
  "UmbracoCommunity.BlogAnnouncements.Modal.PostDetails",
  {
    modal: {
      type: "sidebar",
      size: "medium",
    },
  },
);
