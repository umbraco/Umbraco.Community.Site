using Joonasw.AspNetCore.SecurityHeaders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.Web.Models.Pages.Testing;
using UmbracoCommunity.Web.ViewModelBuilders;
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
            builder.Services.AddScoped<IViewModelBuilder<TestHomePageViewModel>, TestHomePageViewModelBuilder>();

            return builder;
        }
    }
}
