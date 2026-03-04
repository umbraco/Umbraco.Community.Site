using Joonasw.AspNetCore.SecurityHeaders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Configuration;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Services;
using UmbracoCommunity.Web.ViewModelBuilders;
using UmbracoCommunity.Web.ViewModelBuilders.Components;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;
using UmbracoCommunity.Web.ViewModelBuilders.Schema;

namespace UmbracoCommunity.Web.Extensions
{
    /// <summary>
    /// Output cache policy names for API endpoints.
    /// </summary>
    public static class OutputCachePolicies
    {
        /// <summary>
        /// Cache for Umbraco content-driven APIs (blog posts, articles, etc.).
        /// Uses a long duration but is invalidated via Umbraco notifications when content changes.
        /// Tagged with <see cref="OutputCacheTags.BlogContent"/> for targeted eviction.
        /// </summary>
        public const string ContentDriven = "ContentDriven";

        /// <summary>
        /// Cache for external integrations (Sessionize, etc.) where we don't control data changes.
        /// Uses time-based expiration since we can't know when external data changes.
        /// Duration configurable via OutputCache:ExternalApiDurationSeconds (default: 300 seconds / 5 minutes).
        /// </summary>
        public const string ExternalApi = "ExternalApi";
    }

    /// <summary>
    /// Output cache tags for targeted cache eviction.
    /// </summary>
    public static class OutputCacheTags
    {
        /// <summary>
        /// Tag for blog-related content (articles, blog pages).
        /// Evicted when Article or Blog content is published/unpublished.
        /// </summary>
        public const string BlogContent = "blog-content";
    }

    public static class UmbracoBuilderExtensions
    {
        public static IUmbracoBuilder AddOutputCaching(this IUmbracoBuilder builder)
        {
            // Bind configuration with defaults
            var cacheOptions = new OutputCacheOptions();
            builder.Config.GetSection(OutputCacheOptions.SectionName).Bind(cacheOptions);

            builder.Services.AddOutputCache(options =>
            {
                // Content-driven cache: long duration, invalidated by Umbraco notifications
                options.AddPolicy(OutputCachePolicies.ContentDriven, policy =>
                    policy
                        .Expire(TimeSpan.FromSeconds(cacheOptions.ContentDrivenDurationSeconds))
                        .Tag(OutputCacheTags.BlogContent));

                // External API cache: time-based expiration for data we don't control
                options.AddPolicy(OutputCachePolicies.ExternalApi, policy =>
                    policy.Expire(TimeSpan.FromSeconds(cacheOptions.ExternalApiDurationSeconds)));
            });

            return builder;
        }

        public static IUmbracoBuilder AddSecurityPolicies(this IUmbracoBuilder builder)
        {
            builder.Services.AddCsp();
            builder.Services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromSeconds(31536000); // 1 year, minimum recommended https://www.upguard.com/blog/hsts
            });

            return builder;
        }

        public static IUmbracoBuilder AddViewModelBuildersAndDecorators(this IUmbracoBuilder builder)
        {
            // Register shared utilities
            builder.Services.AddScoped<Utilities.ReleaseDiscussionParser>();
            builder.Services.AddScoped<IContentDataService, ContentDataService>();

            // Utilities
            builder.Services.AddScoped<Utilities.UrlUtilities>();

            // Schema builders
            builder.Services.AddScoped<OrganizationSchemaBuilder>();
            builder.Services.AddScoped<ArticleSchemaBuilder>();
            builder.Services.AddScoped<BreadcrumbSchemaBuilder>();

            builder.Services.AddScoped<IPageViewModelDecorator<Seo>, SeoMetaDataViewModelDecorator>();

            builder.Services.AddScoped<IViewModelBuilder<HomePageViewModel>, HomePageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<ArticlePageViewModel>, ArticlePageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<ContentPageViewModel>, ContentPageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<BlogPageViewModel>, BlogPageViewModelBuilder>();
            // Register builders as both interface and concrete types
            builder.Services.AddScoped<ReleasesHomePageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<ReleasesHomePageViewModel>>(sp => sp.GetRequiredService<ReleasesHomePageViewModelBuilder>());

            builder.Services.AddScoped<IViewModelBuilder<ReleasePageViewModel>, ReleasePageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<AllReleasesPageViewModel>, AllReleasesPageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<ComparePageViewModel>, ComparePageViewModelBuilder>();

            builder.Services.AddScoped<IViewModelBuilder<MenuViewModel>, MenuViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<MenuReleasesViewModel>, MenuReleasesViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<FooterViewModel>, FooterViewModelBuilder>();

            return builder;
        }
    }
}
