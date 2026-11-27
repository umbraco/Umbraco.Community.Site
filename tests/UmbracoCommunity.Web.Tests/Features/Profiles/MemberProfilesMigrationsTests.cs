using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class MemberProfilesMigrationsTests
{
    /// <summary>
    /// The same migrations run on SQLite locally and SQL Server in the cloud, so none of them may name a
    /// column type: EF's tooling writes SQLite's (<c>TEXT</c>, <c>INTEGER</c>) when run against the SQLite
    /// design-time database, which SQL Server turns into <c>text</c> columns that reject dates.
    /// </summary>
    [Fact]
    public void No_migration_names_a_provider_specific_column_type()
    {
        using var database = new MemberProfilesTestDatabase();
        using var context = database.CreateDbContext();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();

        var offenders = migrationsAssembly.Migrations
            .SelectMany(m => migrationsAssembly.CreateMigration(m.Value, context.Database.ProviderName!).UpOperations
                .OfType<ColumnOperation>()
                .Where(c => c.ColumnType != null)
                .Select(c => $"{m.Key}: {c.Name} ({c.ColumnType})"))
            .ToList();

        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// Profiles used to show the cached GitHub company, bio and name when the member's own were null. The
    /// <c>CopyGitHubTextIntoOwnProfileText</c> migration copies them in, so nothing disappears now the fallback is gone.
    /// </summary>
    [Fact]
    public async Task Copying_GitHub_text_keeps_what_each_profile_showed_before()
    {
        using var database = new MemberProfilesTestDatabase();
        await using (var context = database.CreateDbContext())
        {
            await context.GetService<IMigrator>().MigrateAsync("AddShownForumBadges");
        }

        await using (var command = database.Connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO MemberProfiles (MemberKey, GitHubHandle, DisplayName, DisplayNameOverride, Headline, Bio, GitHubBio, GitHubCompany,
                    OnboardingStatus, IsProfileVisible, HiddenSections, HiddenActivityIds, ShownForumBadgeIds, HideActivityGraph, HideTimelineCards,
                    NoCardActivityIds, CreatedUtc, UpdatedUtc)
                VALUES
                    ('00000000-0000-0000-0000-000000000001', 'fallback', 'GitHub Name', NULL, NULL, NULL, 'GitHub bio', 'umbraco', 2, 1, '[]', '[]', '[]', 0, 0, '[]', '2026-01-01', '2026-01-01'),
                    ('00000000-0000-0000-0000-000000000002', 'own', 'GitHub Name', 'Own Name', 'Own headline', 'Own bio', 'GitHub bio', 'umbraco', 2, 1, '[]', '[]', '[]', 0, 0, '[]', '2026-01-01', '2026-01-01'),
                    ('00000000-0000-0000-0000-000000000003', 'hidden', 'GitHub Name', 'Own Name', '', NULL, NULL, 'umbraco', 2, 1, '[]', '[]', '[]', 0, 0, '[]', '2026-01-01', '2026-01-01'),
                    ('00000000-0000-0000-0000-000000000004', 'nothing', 'GitHub Name', NULL, NULL, NULL, NULL, '', 2, 1, '[]', '[]', '[]', 0, 0, '[]', '2026-01-01', '2026-01-01');
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using (var context = database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var store = new UmbracoCommunity.Web.Features.Profiles.Data.MemberProfileStore(database);
        var fallback = (await store.GetByHandleAsync("fallback"))!;
        fallback.Headline.Should().Be("umbraco");
        fallback.Bio.Should().Be("GitHub bio");
        fallback.DisplayNameOverride.Should().Be("GitHub Name");

        var own = (await store.GetByHandleAsync("own"))!;
        own.Headline.Should().Be("Own headline");
        own.Bio.Should().Be("Own bio");
        own.DisplayNameOverride.Should().Be("Own Name");

        // A headline the member had chosen to hide stays hidden, now as null.
        var hidden = (await store.GetByHandleAsync("hidden"))!;
        hidden.Headline.Should().BeNull();
        hidden.Bio.Should().BeNull();

        var nothing = (await store.GetByHandleAsync("nothing"))!;
        nothing.Headline.Should().BeNull();
        nothing.Bio.Should().BeNull();
        nothing.DisplayNameOverride.Should().Be("GitHub Name");
    }
}
