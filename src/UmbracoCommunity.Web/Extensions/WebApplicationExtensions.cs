using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using UmbracoCommunity.Web.Middleware;

namespace UmbracoCommunity.Web.Extensions
{
    public static class WebApplicationExtensions
    {
        public static IUmbracoBuilder AddPipelineFilters(this IUmbracoBuilder builder)
        {
            builder.Services.Configure<UmbracoPipelineOptions>(options =>
            {
                options.AddFilter(new UmbracoPipelineFilter("DisableCspFilter", postPipeline: app => app.UseMiddleware<DisableCspMiddleware>()));
                options.AddFilter(new UmbracoPipelineFilter("BlogFolderRedirect", postPipeline: app => app.UseMiddleware<BlogFolderRedirectMiddleware>()));
                options.AddFilter(new UmbracoPipelineFilter("BlogRss", prePipeline: app => app.UseMiddleware<BlogRssMiddleware>()));
            });

            return builder;
        }

        public static void UseSecurityHeaders(this WebApplication app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("X-Xss-Protection", "1; mode=block");
                context.Response.Headers.Append("Referrer-Policy", "no-referrer-when-downgrade");
                context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Remove("X-Powered-By");

                // The following was generated at https://www.permissionspolicy.com/:
                // - no permissions were selected for all features
                // - apart from the following found to be in use: unload, clipboard-write (for share functionality)
                // - removed references to the following features that throw unrecognised warnings in the browser console: ambient-light-sensor, battery, document-domain, execution-while-not-rendered, execution-while-out-of-viewport, navigation-override, speaker-selection, conversion-measurement, focus-without-user-activation, sync-script, trust-token-redemption, window-placement, vertical-scroll
                const string PermissionsPolicyValue = "accelerometer=(), autoplay=(), camera=(), cross-origin-isolated=(), display-capture=(), encrypted-media=(), fullscreen=(self), geolocation=(), gyroscope=(), keyboard-map=(), magnetometer=(), microphone=(), midi=(), payment=(), picture-in-picture=(), publickey-credentials-get=(), screen-wake-lock=(), sync-xhr=(), usb=(), web-share=(), xr-spatial-tracking=(), clipboard-read=(), clipboard-write=(self), gamepad=(), hid=(), idle-detection=(), interest-cohort=(), serial=(), unload=(self)";
                context.Response.Headers.Append("Permissions-Policy", PermissionsPolicyValue);

                await next();
            });

            app.UseCsp(csp =>
            {
                SetProductionCspRules(csp);

                if (app.Environment.IsDevelopment())
                {
                    SetDevelopmentCspRules(csp);
                }

                csp.OnSendingHeader = context =>
                {
                    context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/umbraco");
                    return Task.CompletedTask;
                };
            });
        }

        private static void SetProductionCspRules(CspBuilder csp)
        {
            csp.ByDefaultAllow
                .FromSelf()
                .FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowDomains);

            csp.AllowConnections
                .ToAll(Constants.Security.DefaultAllowConnections)
                .ToSelf();

            csp.AllowFraming.FromSelf();

            csp.AllowBaseUri.FromSelf();

            csp.AllowFrames.FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowFrames);

            csp.AllowScripts
                .FromSelf()
                .FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowScripts)
                .AddNonce();

            csp.AllowStyles
                .FromSelf()
                .FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowStyles)
                .AllowUnsafeInline();

            csp.AllowFonts
                .FromSelf()
                .FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowFonts);

            csp.AllowImages
                .FromSelf()
                .FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowImages);

            csp.AllowWorkers
                .FromAll((builder, domains) => builder.From(domains), Constants.Security.DefaultAllowWorkers);

            csp.AllowFormActions
                .ToSelf()
                .FromAll((builder, domain) => builder.To(domain), Constants.Security.DefaultAllowFormActions);

            csp.AllowAudioAndVideo
                .FromSelf()
                .FromAll((builder, domains) => builder.From(domains), Constants.Security.DefaultAllowMedia);
        }

        private static void SetDevelopmentCspRules(CspBuilder csp)
        {
            string[] developmentDomains = ["localhost:*", "*.umbraco.com", "debugger:*"];

            csp.AllowConnections.ToAll("ws://localhost:*", "localhost:*");

            csp.AllowScripts.FromAll((builder, domain) => builder.From(domain), developmentDomains);

            csp.AllowStyles.FromAll((builder, domain) => builder.From(domain), developmentDomains);

            csp.AllowImages.FromAll((builder, domain) => builder.From(domain), developmentDomains);
        }
    }
}
