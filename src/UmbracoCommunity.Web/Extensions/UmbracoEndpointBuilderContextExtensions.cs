using Microsoft.AspNetCore.Builder;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using UmbracoCommunity.Web.Features.OpenBadges;

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

            // Register route for the signed Web Bot Auth key directory (Cloudflare verified bots)
            builder.EndpointRouteBuilder.MapControllerRoute(
              "WebBotAuthDirectory",
              ".well-known/http-message-signatures-directory",
              new
              {
                  controller = "WebBotAuthDirectory",
                  action = "Index"
              });

            // Register routes for the Open Badges 2.0 "Umbraco MVP" badges (issuer, badge classes,
            // hosted assertions, artwork, and the signed-in member's baked download)
            MapOpenBadgesRoute(builder, OpenBadgesRoutes.Names.Issuer, OpenBadgesRoutes.IssuerPattern, "Issuer");
            MapOpenBadgesRoute(builder, OpenBadgesRoutes.Names.MvpBadgeClass, OpenBadgesRoutes.MvpBadgeClassPattern, "MvpBadgeClass");
            MapOpenBadgesRoute(builder, OpenBadgesRoutes.Names.MvpImage, OpenBadgesRoutes.MvpImagePattern, "MvpImage");
            MapOpenBadgesRoute(builder, OpenBadgesRoutes.Names.Assertion, OpenBadgesRoutes.AssertionPattern, "Assertion");
            MapOpenBadgesRoute(builder, OpenBadgesRoutes.Names.MvpDownload, OpenBadgesRoutes.MvpDownloadPattern, "MvpDownload");

            // Register route for the aggregated community blogs RSS feed
            builder.EndpointRouteBuilder.MapControllerRoute(
              "CommunityBlogsFeed",
              "feeds/community-blogs.xml",
              new
              {
                  controller = "CommunityBlogsFeed",
                  action = "Index"
              });

            return builder;
        }

        private static void MapOpenBadgesRoute(IUmbracoEndpointBuilderContext builder, string name, string pattern, string action) =>
            builder.EndpointRouteBuilder.MapControllerRoute(
              name,
              pattern,
              new
              {
                  controller = OpenBadgesRoutes.Controller,
                  action
              });
    }
}
