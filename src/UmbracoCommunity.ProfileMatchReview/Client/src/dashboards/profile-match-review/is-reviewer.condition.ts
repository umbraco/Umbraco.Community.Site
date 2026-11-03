import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
  UmbConditionConfigBase,
  UmbConditionControllerArguments,
  UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { hasReviewAccess, setAuthConfig } from "./api/profile-match-review-api.js";

export const REVIEWER_CONDITION_ALIAS = "UmbracoCommunity.ProfileMatchReview.Condition.IsReviewer";

export type IsReviewerConditionConfig = UmbConditionConfigBase<typeof REVIEWER_CONDITION_ALIAS>;

/** Permitted only when the server says the signed-in user is in the Profile match review group. */
export class IsReviewerCondition
  extends UmbConditionBase<IsReviewerConditionConfig>
  implements UmbExtensionCondition
{
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<IsReviewerConditionConfig>) {
    super(host, args);

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const config = authContext.getOpenApiConfiguration();
      setAuthConfig({
        token: () => authContext.getLatestToken(),
        baseUrl: config.base,
        credentials: config.credentials as RequestCredentials,
      });
      this.permitted = await hasReviewAccess();
    });
  }
}

export { IsReviewerCondition as api };
