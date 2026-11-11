using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Recording;

namespace UmbracoCommunity.SearchTracker.Tests;

public class SearchEventWriterServiceTests : SqliteTestBase
{
    private static readonly Guid SiteA = Guid.NewGuid();
    private static readonly Guid SiteB = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 11, 4, 23, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);

    private (SearchEventWriterService Writer, SearchEventChannel Channel) Create()
    {
        var options = TestOptions.Create();
        var channel = new SearchEventChannel(options);
        var writer = new SearchEventWriterService(channel, Factory, new SearchTrackerSchemaState(), options,
            NullLogger<SearchEventWriterService>.Instance);
        return (writer, channel);
    }

    [Fact]
    public async Task Groups_a_batch_into_one_row_per_site_day_and_term()
    {
        var (writer, channel) = Create();
        channel.Writer.TryWrite(new SearchEvent(SiteA, "forms", 2, Day1));
        channel.Writer.TryWrite(new SearchEvent(SiteA, "forms", 0, Day1));
        channel.Writer.TryWrite(new SearchEvent(SiteA, "forms", 0, Day2));
        channel.Writer.TryWrite(new SearchEvent(SiteB, "forms", 4, Day1));

        await writer.DrainOnceAsync(int.MaxValue, CancellationToken.None);

        using var context = CreateContext();
        var rows = context.SearchTermDaily.OrderBy(r => r.Day).ThenBy(r => r.SearchCount).ToList();
        rows.Should().HaveCount(3);
        rows.Should().ContainSingle(r => r.SiteKey == SiteA && r.Day == new DateOnly(2026, 11, 4))
            .Which.Should().Match<Models.Entities.SearchTermDailyEntity>(r => r.SearchCount == 2 && r.ZeroResultCount == 1 && r.LastResultCount == 0);
        rows.Should().ContainSingle(r => r.SiteKey == SiteA && r.Day == new DateOnly(2026, 11, 5))
            .Which.ZeroResultCount.Should().Be(1);
        rows.Should().ContainSingle(r => r.SiteKey == SiteB).Which.ZeroResultCount.Should().Be(0);
    }

    [Fact]
    public async Task Adds_to_an_existing_row()
    {
        Seed(Row(SiteA, "2026-11-05", "forms", searches: 5, zero: 1));
        var (writer, channel) = Create();
        channel.Writer.TryWrite(new SearchEvent(SiteA, "forms", 0, Day2));
        channel.Writer.TryWrite(new SearchEvent(SiteA, "forms", 7, Day2));

        await writer.DrainOnceAsync(int.MaxValue, CancellationToken.None);

        using var context = CreateContext();
        var row = context.SearchTermDaily.Single();
        row.SearchCount.Should().Be(7);
        row.ZeroResultCount.Should().Be(2);
        row.LastResultCount.Should().Be(7);
    }
}
