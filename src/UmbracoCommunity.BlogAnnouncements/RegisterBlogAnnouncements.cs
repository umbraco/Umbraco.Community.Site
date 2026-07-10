using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.BlogAnnouncements.Api;
using UmbracoCommunity.BlogAnnouncements.Dashboard;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Detection;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;

namespace UmbracoCommunity.BlogAnnouncements;

/// <summary>
/// Wires up the Discord blog-announcement pipeline: options, the EF Core tracking store
/// (provider-switched on the Umbraco connection string), the startup migration service, the
/// delivery leg, and the detection service. Detection is invoked exclusively by the
/// <c>PollBlogPostsAction</c> / <c>AnnounceBlogPostsAction</c> Umbraco Automate actions — not by
/// the host's own CommunityBlogs refresh cycle, which only maintains the site's blog-listing
/// cache/index.
/// </summary>
public sealed class RegisterBlogAnnouncements : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<BlogAnnouncementsOptions>(
            builder.Config.GetSection(BlogAnnouncementsOptions.SectionName));

        builder.Services.AddDbContextFactory<BlogAnnouncementsDbContext>((sp, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var dataDir = Path.Combine(env.ContentRootPath, "umbraco", "Data");
                var resolved = connectionString?.Replace("|DataDirectory|", dataDir);
                options.UseSqlite(resolved, sqlite =>
                    sqlite.MigrationsAssembly("UmbracoCommunity.BlogAnnouncements"));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("UmbracoCommunity.BlogAnnouncements");
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddHostedService<BlogAnnouncementsMigrationHostedService>();

        // Delivery leg (fallback path). Behind IDiscordAnnouncer so an Automate trigger can
        // replace it later without touching detection.
        builder.Services.AddHttpClient<DiscordAnnouncerHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });
        builder.Services.AddSingleton<IDiscordAnnouncer, DiscordWebhookAnnouncer>();

        // Detection. Singleton; opens a fresh DbContext per cycle via the factory. Invoked
        // exclusively by the PollBlogPostsAction / AnnounceBlogPostsAction Automate actions.
        builder.Services.AddSingleton<IBlogAnnouncementDetector, BlogAnnouncementDetector>();

        // Dashboard query + manual-dispatch service. Singleton so its in-flight guard is shared
        // across requests; it opens a fresh DbContext per call via the factory.
        builder.Services.AddSingleton<BlogAnnouncementDashboardService>();

        // OpenAPI document for the backoffice management API (mirrors NotFoundTracker /
        // BlockRestrictions on Umbraco 18). WithBackOfficeAuthentication() wires up the Umbraco
        // backoffice "Authorize" button in Swagger UI so the endpoints can be tested while
        // authenticated.
        builder.AddBackOfficeOpenApiDocument(BlogAnnouncementsApiConstants.ApiName, doc => doc
            .WithTitle("Umbraco Community Blog Announcements Backoffice API")
            .WithBackOfficeAuthentication());
    }
}
