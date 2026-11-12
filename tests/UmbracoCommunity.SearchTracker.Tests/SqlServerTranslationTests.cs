using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Reporting;

namespace UmbracoCommunity.SearchTracker.Tests;

// Translates queries and migrations for SQL Server without a server, so provider-specific failures show up in CI.
public class SqlServerTranslationTests
{
    private static SearchTrackerDbContext CreateSqlServerContext() => new(
        new DbContextOptionsBuilder<SearchTrackerDbContext>()
            .UseSqlServer("Server=localhost;Database=SearchTrackerTranslation;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly("UmbracoCommunity.SearchTracker"))
            .Options);

    private static readonly ReportFilter Filter = new(Guid.NewGuid(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

    [Theory]
    [InlineData(TermSort.Searches, false)]
    [InlineData(TermSort.FewestResults, true)]
    [InlineData(TermSort.LastSearched, false)]
    [InlineData(TermSort.Term, false)]
    public void Term_queries_translate(TermSort sort, bool nothingFoundOnly)
    {
        using var context = CreateSqlServerContext();
        var query = new TermQuery("forms", nothingFoundOnly, sort, 25, 25);

        var sql = SearchReportService.OrderTerms(SearchReportService.BuildTermsQuery(context, Filter, query, SiteScope.Full), sort)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToQueryString();

        sql.Should().Contain("SearchTrackerTermDaily");
    }

    [Fact]
    public void Migrations_generate_a_sql_server_script()
    {
        using var context = CreateSqlServerContext();

        var script = context.GetService<IMigrator>().GenerateScript();

        script.Should().Contain("CREATE TABLE [SearchTrackerTermDaily]").And.Contain("[LastResultCount] int NULL");
    }
}
