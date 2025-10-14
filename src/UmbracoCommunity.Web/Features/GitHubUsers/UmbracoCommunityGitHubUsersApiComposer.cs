using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Api.Common.OpenApi;

namespace UmbracoCommunity.Web.Features.GitHubUsers;

public class UmbracoCommunityGitHubUsersApiComposer : IComposer
{
    private const string ApiName = "umbracocommunitygithubusers";
        
    public void Compose(IUmbracoBuilder builder)
    {

        builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

        builder.Services.Configure<SwaggerGenOptions>(opt =>
        {
            // Configure the Swagger generation options
            // Add in a new Swagger API document solely for our own package that can be browsed via Swagger UI
            // Along with having a generated swagger JSON file that we can use to auto generate a TypeScript client
            opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "Umbraco Community Git Hub Users Backoffice API",
                Version = "1.0",
            });

            // Enable Umbraco authentication for the "Example" Swagger document
            // PR: https://github.com/umbraco/Umbraco-CMS/pull/15699
            opt.OperationFilter<UmbracoCommunityGitHubUsersOperationSecurityFilter>();
        });
    }

    public class UmbracoCommunityGitHubUsersOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }

    // This is used to generate nice operation IDs in our swagger json file
    // So that the generated TypeScript client has nice method names and not too verbose
    // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/umbraco-schema-and-operation-ids#operation-ids
    public class CustomOperationHandler : OperationIdHandler
    {
        public CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions) : base(apiVersioningOptions)
        {
        }

        protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
        {
            return controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith("UmbracoCommunity.Web.Features.GitHubUsers.Api", comparisonType: StringComparison.InvariantCultureIgnoreCase) is true;
        }

        public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
    }
}