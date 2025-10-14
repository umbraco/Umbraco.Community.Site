using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Cms.Web.Common.Routing;

namespace UmbracoCommunity.Web.Features.ReleaseOverview.Configuration;

public class ReleaseRouteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Configure /release/ paths to be treated as server-side requests (not client-side)
        // This ensures Umbraco context is available for IVirtualPageController
        builder.Services.Configure<UmbracoRequestOptions>(options =>
        {
            options.HandleAsServerSideRequest = httpRequest =>
                httpRequest.Path.StartsWithSegments("/release");
        });

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(nameof(ReleaseRouteComposer))
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute(
                        name: "Release",
                        pattern: "release/{org}/{repo}/{version}",
                        defaults: new { controller = "Release", action = "Index" });
                })
            });
        });
    }
}
