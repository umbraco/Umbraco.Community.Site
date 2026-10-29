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
}
