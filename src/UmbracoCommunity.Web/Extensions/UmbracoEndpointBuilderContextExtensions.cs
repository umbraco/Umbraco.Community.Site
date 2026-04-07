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

            // Register route for security.txt (RFC 9116)
            builder.EndpointRouteBuilder.MapControllerRoute(
              "SecurityTxt",
              ".well-known/security.txt",
              new
              {
                  controller = "SecurityTxt",
                  action = "Index"
              });

            return builder;
        }
    }
}
