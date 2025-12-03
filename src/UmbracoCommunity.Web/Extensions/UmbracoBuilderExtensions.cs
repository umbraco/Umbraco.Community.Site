using Joonasw.AspNetCore.SecurityHeaders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Services;
using UmbracoCommunity.Web.ViewModelBuilders;
using UmbracoCommunity.Web.ViewModelBuilders.Components;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Web.Extensions
{
    public static class UmbracoBuilderExtensions
    {
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

            builder.Services.AddScoped<IViewModelBuilder<HomePageViewModel>, HomePageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<ArticlePageViewModel>, ArticlePageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<ContentPageViewModel>, ContentPageViewModelBuilder>();
            builder.Services.AddScoped<IViewModelBuilder<BlogPageViewModel>, BlogPageViewModelBuilder>();

            return builder;
        }
    }
}
