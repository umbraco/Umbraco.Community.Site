using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UmbracoCommunity.Web.Features.WebBotAuth;

public sealed class RegisterWebBotAuth : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<WebBotAuthOptions>(builder.Config.GetSection(WebBotAuthOptions.SectionName));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<KeyDirectoryService>();
    }
}
