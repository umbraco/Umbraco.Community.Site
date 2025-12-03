using Microsoft.AspNetCore.Builder;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace UmbracoCommunity.Web.Extensions
{
    public static class UmbracoEndpointBuilderContextExtensions
    {
        public static IUmbracoEndpointBuilderContext UseApplicationEndpoints(this IUmbracoEndpointBuilderContext builder)
        {
            if (!builder.RuntimeState.UmbracoCanBoot())
            {
                return builder;
            }

            // Register route for robots.txt
            builder.EndpointRouteBuilder.MapControllerRoute(
              "RobotsTxt",
              "robots.txt",
              new
              {
                  controller = "Robots",
                  action = "Index"
              });

            return builder;
        }
    }
}
