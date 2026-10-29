import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
  UmbConditionConfigBase,
  UmbConditionControllerArguments,
  UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_MEMBER_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/member";
import type { Observable } from "@umbraco-cms/backoffice/external/rxjs";

export const IS_EXTERNAL_MEMBER_CONDITION_ALIAS =
  "UmbracoCommunity.Condition.IsExternalMember";

export type IsExternalMemberConditionConfig =
  UmbConditionConfigBase<typeof IS_EXTERNAL_MEMBER_CONDITION_ALIAS>;

/**
 * Value of the member workspace's `kind` for lightweight external-only members
 * (MemberKind.ExternalOnly in the Management API).
 */
const EXTERNAL_ONLY_KIND = "ExternalOnly";

/**
 * Permits only when the member open in the member workspace is an external-only member.
 *
 * Umbraco 18.3's member workspace context exposes a `kind` observable, but the
 * `@umbraco-cms/backoffice` typings this project builds against predate it, so it's read
 * defensively. If it's missing at runtime the condition simply never permits.
 */
export class IsExternalMemberCondition
  extends UmbConditionBase<IsExternalMemberConditionConfig>
  implements UmbExtensionCondition
{
  constructor(
    host: UmbControllerHost,
    args: UmbConditionControllerArguments<IsExternalMemberConditionConfig>
  ) {
    super(host, args);

    this.consumeContext(UMB_MEMBER_WORKSPACE_CONTEXT, (context) => {
      const kind = (context as { kind?: Observable<string | undefined> } | undefined)?.kind;
      if (!kind) {
        this.permitted = false;
        return;
      }

      this.observe(
        kind,
        (value) => {
          // `kind` is briefly undefined while the workspace reloads (e.g. after saving groups).
          // Revoking permission then would unmount the view and leave the route blank, so only
          // react to a real value.
          if (value === undefined) return;
          this.permitted = value === EXTERNAL_ONLY_KIND;
        },
        "_observeMemberKind"
      );
    });
  }
}

declare global {
  interface UmbExtensionConditionConfigMap {
    IsExternalMemberConditionConfig: IsExternalMemberConditionConfig;
  }
}

export { IsExternalMemberCondition as api };
