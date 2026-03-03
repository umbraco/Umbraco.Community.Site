import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
  UmbConditionConfigBase,
  UmbConditionControllerArguments,
  UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_ENTITY_CONTEXT } from "@umbraco-cms/backoffice/entity";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UmbracoCommunityExtensionsService } from "../api/index.js";
import { client } from "../api/client.gen.js";

export const IS_BLOG_NODE_CONDITION_ALIAS =
  "UmbracoCommunity.Condition.IsBlogNode";

export type IsBlogNodeConditionConfig =
  UmbConditionConfigBase<typeof IS_BLOG_NODE_CONDITION_ALIAS>;

/**
 * A condition that checks if the current entity is a Blog document type.
 * Consumes UMB_AUTH_CONTEXT to configure the API client with bearer token
 * authentication before making SDK calls.
 */
export class IsBlogNodeCondition
  extends UmbConditionBase<IsBlogNodeConditionConfig>
  implements UmbExtensionCondition
{
  #authReady = false;
  #currentUnique?: string;

  constructor(
    host: UmbControllerHost,
    args: UmbConditionControllerArguments<IsBlogNodeConditionConfig>
  ) {
    super(host, args);

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      client.setConfig({
        auth: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
      this.#authReady = true;

      // If entity context resolved before auth, make the deferred API call now.
      if (this.#currentUnique) {
        this.#checkIsBlog(this.#currentUnique);
      }
    });

    this.consumeContext(UMB_ENTITY_CONTEXT, (entityContext) => {
      if (!entityContext) {
        this.permitted = false;
        return;
      }

      this.observe(
        entityContext.unique,
        async (unique) => {
          if (!unique) {
            this.permitted = false;
            return;
          }

          this.#currentUnique = unique;

          // Only call the API once auth is configured.
          if (this.#authReady) {
            await this.#checkIsBlog(unique);
          }
        },
        "uniqueObserver"
      );
    });
  }

  async #checkIsBlog(nodeKey: string): Promise<void> {
    try {
      const { data, error } =
        await UmbracoCommunityExtensionsService.isBlogNode({
          path: { nodeKey },
        });

      if (error) {
        this.permitted = false;
        return;
      }

      this.permitted = data === true;
    } catch {
      this.permitted = false;
    }
  }
}

// Declare the Condition Configuration Type in the global UmbExtensionConditionConfigMap interface
declare global {
  interface UmbExtensionConditionConfigMap {
    IsBlogNodeConditionConfig: IsBlogNodeConditionConfig;
  }
}

export { IsBlogNodeCondition as api };
