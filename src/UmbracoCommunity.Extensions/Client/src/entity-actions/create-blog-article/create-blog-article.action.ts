import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_DOCUMENT_WORKSPACE_PATH } from "@umbraco-cms/backoffice/document";
import { UmbracoCommunityExtensionsService } from "../../api/index.js";
import { client } from "../../api/client.gen.js";

export class CreateBlogArticleAction extends UmbEntityActionBase<never> {
  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor(host: any, args: any) {
    super(host, args);
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (instance) => {
      this.#notificationContext = instance;
    });
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      client.setConfig({
        auth: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
    });
  }

  override async execute() {
    const blogNodeKey = this.args.unique;

    if (!blogNodeKey) {
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Error",
          message: "Could not determine blog node key",
        },
      });
      return;
    }

    try {
      const { data, error } =
        await UmbracoCommunityExtensionsService.createBlogArticle({
          path: { blogNodeKey },
        });

      if (error) {
        // Check if it's a "not a blog" error and show a friendlier message
        const errorText =
          typeof error === "string" ? error : JSON.stringify(error);
        if (errorText.includes("not a blog")) {
          this.#notificationContext?.peek("warning", {
            data: {
              headline: "Not Available",
              message: "This action is only available on Blog nodes",
            },
          });
          return;
        }

        throw new Error(errorText);
      }

      if (data) {
        this.#notificationContext?.peek("positive", {
          data: {
            headline: "Article Created",
            message: `"${data.articleName}" has been created`,
          },
        });

        // Navigate to the new article
        if (data.articleKey) {
          const editPath = `${UMB_DOCUMENT_WORKSPACE_PATH}/edit/${data.articleKey}`;
          window.history.pushState({}, "", editPath);
          window.dispatchEvent(new PopStateEvent("popstate"));
        }
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error occurred";

      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Failed to Create Article",
          message: errorMessage,
        },
      });
    }
  }
}

export { CreateBlogArticleAction as api };
