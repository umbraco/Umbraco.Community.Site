using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UmbracoCommunity.Extensions.Composers
{
    public class UmbracoCommunityExtensionsApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            // Related documentation:
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/adding-a-custom-swagger-document
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/versioning-your-api
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/access-policies

            // Add a dedicated OpenAPI document for our own package that can be browsed via Swagger UI,
            // along with a generated swagger JSON file used to auto-generate a TypeScript client.
            // WithBackOfficeAuthentication() enables Umbraco backoffice auth for this document.
            builder.AddBackOfficeOpenApiDocument(Constants.ApiName, doc => doc
                .WithTitle("Umbraco Community Extensions Backoffice API")
                .WithBackOfficeAuthentication()
                .ConfigureOpenApiOptions(options =>
                    // Registered after Umbraco's default UmbracoOperationIdTransformer so it wins,
                    // giving our controllers concise operation IDs (just the action name).
                    options.AddOperationTransformer<CustomOperationIdTransformer>()));
        }

        // Generates nice, short operation IDs for our controllers so the generated TypeScript
        // client has concise (not overly verbose) method names.
        // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/umbraco-schema-and-operation-ids#operation-ids
        private sealed class CustomOperationIdTransformer : IOpenApiOperationTransformer
        {
            public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
            {
                if (context.Description.ActionDescriptor is ControllerActionDescriptor descriptor &&
                    descriptor.ControllerTypeInfo.Namespace?.StartsWith("UmbracoCommunity.Extensions.Controllers", StringComparison.InvariantCultureIgnoreCase) is true)
                {
                    operation.OperationId = descriptor.ActionName;
                }

                return Task.CompletedTask;
            }
        }
    }
}
