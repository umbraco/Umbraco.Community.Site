using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Recording;
using UmbracoCommunity.SearchTracker.Reporting;

namespace UmbracoCommunity.SearchTracker.Tests;

public class SearchReportServiceTests : SqliteTestBase
{
    private static readonly Guid SiteA = Guid.NewGuid();
    private static readonly Guid SiteB = Guid.NewGuid();
    private static readonly ReportFilter AllSites = new(null, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

    public SearchReportServiceTests()
    {
        Seed(
            Row(SiteA, "2026-11-01", "forms", 4, 0),
            Row(SiteA, "2026-11-03", "forms", 2, 0),
            Row(SiteA, "2026-11-03", "headless", 3, 3),
            Row(SiteA, "2026-10-01", "old term", 9, 9),
            Row(SiteB, "2026-11-05", "tickets", 6, 1),
            Row(SiteB, "2026-11-05", "forms", 1, 0));
    }

    private SearchReportService Service => new(Factory);

    private static TermQuery Query(TermSort sort = TermSort.Searches, bool zeroOnly = false, string? contains = null)
        => new(contains, zeroOnly, sort, 0, 25);

    [Fact]
    public async Task Summary_totals_the_range_and_fills_empty_days()
    {
        var summary = await Service.GetSummaryAsync(AllSites, SiteScope.Full, CancellationToken.None);

        summary.TotalSearches.Should().Be(16);
        summary.ZeroResultSearches.Should().Be(4);
        summary.UniqueTerms.Should().Be(3);
        summary.Days.Select(d => d.Searches).Should().Equal(4, 0, 5, 0, 7);
    }

    [Fact]
    public async Task Terms_are_aggregated_across_sites_and_days()
    {
        var (items, total) = await Service.GetTermsAsync(AllSites, Query(), SiteScope.Full, CancellationToken.None);

        total.Should().Be(3);
        items.Select(i => (i.Term, i.Searches)).Should().Equal(("forms", 7), ("tickets", 6), ("headless", 3));
        items[0].LastSearched.Should().Be(new DateOnly(2026, 11, 5));
    }

    [Fact]
    public async Task Zero_results_only_returns_terms_with_failed_searches()
    {
        var (items, _) = await Service.GetTermsAsync(AllSites, Query(TermSort.ZeroResults, zeroOnly: true), SiteScope.Full, CancellationToken.None);

        items.Select(i => i.Term).Should().Equal("headless", "tickets");
    }

    [Fact]
    public async Task Contains_filters_terms()
    {
        var (items, total) = await Service.GetTermsAsync(AllSites, Query(contains: "TICK"), SiteScope.Full, CancellationToken.None);

        total.Should().Be(1);
        items.Single().Term.Should().Be("tickets");
    }

    [Fact]
    public async Task Restricted_scope_only_sees_its_sites()
    {
        var scope = new SiteScope(new HashSet<Guid> { SiteB }, hasFullAccess: false);

        var (items, _) = await Service.GetTermsAsync(AllSites, Query(), scope, CancellationToken.None);
        var forbiddenSite = await Service.GetSummaryAsync(AllSites with { SiteKey = SiteA }, scope, CancellationToken.None);

        items.Select(i => i.Term).Should().Equal("tickets", "forms");
        forbiddenSite.TotalSearches.Should().Be(0);
        (await Service.GetSiteKeysAsync(scope, CancellationToken.None)).Should().Equal(SiteB);
    }

    [Fact]
    public async Task Trend_returns_one_entry_per_day()
    {
        var trend = await Service.GetTermTrendAsync(AllSites, "forms", SiteScope.Full, CancellationToken.None);

        trend.Select(d => d.Searches).Should().Equal(4, 0, 2, 0, 1);
    }

    [Fact]
    public async Task Delete_removes_a_term_for_one_site_only()
    {
        var deleted = await Service.DeleteTermAsync(SiteA, "forms", SiteScope.Full, CancellationToken.None);

        deleted.Should().Be(2);
        using var context = CreateContext();
        context.SearchTermDaily.Count(r => r.Term == "forms").Should().Be(1);
    }

    [Fact]
    public async Task Delete_does_nothing_outside_the_scope()
    {
        var scope = new SiteScope(new HashSet<Guid> { SiteB }, hasFullAccess: false);

        var deleted = await Service.DeleteTermAsync(SiteA, "forms", scope, CancellationToken.None);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task Retention_sweep_deletes_rows_older_than_the_window()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var sweeper = new SearchTrackerRetentionService(Factory, new SearchTrackerSchemaState(),
            TestOptions.Create(o => o.RetentionDays = 30), time, NullLogger<SearchTrackerRetentionService>.Instance);

        await sweeper.SweepAsync(CancellationToken.None);

        using var context = CreateContext();
        context.SearchTermDaily.Any(r => r.Term == "old term").Should().BeFalse();
        context.SearchTermDaily.Count().Should().Be(5);
    }
}
