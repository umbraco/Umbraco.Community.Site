using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;

namespace UmbracoCommunity.Web.Features.Seed;

public sealed class RegisterSeedExport : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISeedExportService, SeedExportService>();
        builder.Services.AddHostedService<SeedExportHostedService>();
    }
}
