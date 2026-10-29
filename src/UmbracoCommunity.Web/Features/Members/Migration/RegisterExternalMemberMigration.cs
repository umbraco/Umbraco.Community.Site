using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Features.Members.Migration;

public sealed class RegisterExternalMemberMigration : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<ExternalMemberMigrationOptions>(
            builder.Config.GetSection(ExternalMemberMigrationOptions.SectionName));

        builder.Services.AddSingleton<ExternalMemberMigrationLease>();
        builder.Services.AddScoped<ExternalMemberMigrationService>();

        // Converts content-based members once, shortly after start-up (scheduling publisher / single
        // server only). Switch off with ExternalMemberMigration:RunOnStartup = false.
        builder.Services.AddRecurringBackgroundJob<ExternalMemberMigrationStartupJob>();
    }
}
