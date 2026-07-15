using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Routing;

namespace UmbracoCommunity.Web.Features.Profiles;

public sealed class RegisterMemberProfiles : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddDbContextFactory<MemberProfilesDbContext>((sp, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var dataDir = Path.Combine(env.ContentRootPath, "umbraco", "Data");
                var resolved = connectionString?.Replace("|DataDirectory|", dataDir);
                options.UseSqlite(resolved, sqlite =>
                    sqlite.MigrationsAssembly("UmbracoCommunity.Web"));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("UmbracoCommunity.Web");
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddHostedService<MemberProfilesMigrationHostedService>();

        builder.Services.AddScoped<MemberProfileStore>();
        builder.Services.AddScoped<ProfileAvatarUrlResolver>();
        builder.Services.AddScoped<AvatarUploadService>();
        builder.Services.AddScoped<IProfileDataProvider, MemberProfileDataProvider>();
        builder.Services.AddScoped<ISphereProfileSyncClient, StubSphereProfileSyncClient>();

        // Append so built-in content finders match first. Ours only fires under the tenant's
        // Community Profile page for a plausible-handle trailing segment (e.g. /community/profiles/octocat).
        builder.ContentFinders().Append<CommunityProfileContentFinder>();
    }
}
