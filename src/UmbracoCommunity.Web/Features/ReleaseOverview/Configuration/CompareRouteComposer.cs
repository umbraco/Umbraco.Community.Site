using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace UmbracoCommunity.Web.Features.ReleaseOverview.Configuration;

public class CompareRouteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(nameof(CompareRouteComposer))
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute(
                        name: "Compare",
                        pattern: "compare",
                        defaults: new { controller = "Compare", action = "Index" });
                })
            });
        });
    }
}
