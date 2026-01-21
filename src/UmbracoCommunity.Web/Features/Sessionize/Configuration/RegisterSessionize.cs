using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        
        builder.Services.AddHttpClient("Sessionize", (sp, client) => 
        {
            var options = sp.GetRequiredService<IOptions<SessionizeOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }
}
