using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.ProfileMatchReview.Api;

namespace UmbracoCommunity.ProfileMatchReview;

public sealed class RegisterProfileMatchReview : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // OpenAPI document for the backoffice management API (same shape as the other backoffice packages).
        builder.AddBackOfficeOpenApiDocument(ProfileMatchReviewApiConstants.ApiName, doc => doc
            .WithTitle("Umbraco Community Profile Match Review Backoffice API")
            .WithBackOfficeAuthentication());
    }
}
