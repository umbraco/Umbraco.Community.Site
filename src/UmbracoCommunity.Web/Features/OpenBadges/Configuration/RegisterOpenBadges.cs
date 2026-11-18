using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

namespace UmbracoCommunity.Web.Features.OpenBadges.Configuration;

public sealed class RegisterOpenBadges : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<OpenBadgesOptions>(builder.Config.GetSection(OpenBadgesOptions.SectionName));
        builder.Services.AddSingleton<MvpBadgeTokenService>();
        builder.Services.AddSingleton<MvpBadgeArtwork>();

        // Scoped: they use ProfileActivityService and IPublishedContentQuery, which are scoped.
        builder.Services.AddScoped<IMvpBadgeEligibility, MvpBadgeEligibility>();
        builder.Services.AddScoped<IOpenBadgeRecipientResolver, CommunityProfileRecipientResolver>();
        builder.Services.AddScoped<MvpBadgeService>();
    }
}
