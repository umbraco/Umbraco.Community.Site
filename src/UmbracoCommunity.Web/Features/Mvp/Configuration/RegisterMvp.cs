using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;

namespace UmbracoCommunity.Web.Features.Mvp.Configuration;

public class RegisterMvp : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IMvpDataService, MvpDataService>();
    }
}
