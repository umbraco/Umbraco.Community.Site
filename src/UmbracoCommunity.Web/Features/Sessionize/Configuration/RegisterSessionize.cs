using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.Configuration;

public class RegisterSessionize : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Register configuration options
        builder.Services.Configure<SessionizeOptions>(
            builder.Config.GetSection(SessionizeOptions.SectionName));

        // Register the Sessionize API client as scoped (one per request)
        builder.Services.AddScoped<SessionizeApiClient>();
    }
}
