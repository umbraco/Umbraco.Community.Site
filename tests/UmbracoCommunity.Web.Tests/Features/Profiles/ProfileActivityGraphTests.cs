using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileActivityGraphTests
{
    // A Thursday.
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static DateTimeOffset On(int year, int month, int day) => new(year, month, day, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_graph_covers_twelve_months_in_weeks_starting_on_monday()
    {
        var graph = ProfileActivityGraph.Build([], Today);

        graph.Weeks.Should().OnlyContain(w => w.Start.DayOfWeek == DayOfWeek.Monday);
        graph.Weeks[0].Start.Should().Be(new DateOnly(2025, 11, 10));
        graph.Weeks[^1].Start.Should().Be(new DateOnly(2026, 11, 9));
        graph.Weeks.Should().HaveCount(53);
    }

    [Fact]
    public void Activity_is_counted_in_the_week_it_happened()
    {
        var graph = ProfileActivityGraph.Build(
            [On(2026, 11, 9), On(2026, 11, 12), On(2026, 11, 2), On(2026, 11, 8)],
            Today);

        graph.Weeks[^1].Count.Should().Be(2);
        graph.Weeks[^2].Count.Should().Be(2);
        graph.Weeks.Sum(w => w.Count).Should().Be(4);
    }

    [Fact]
    public void Activity_outside_the_last_twelve_months_is_ignored()
    {
        var graph = ProfileActivityGraph.Build([On(2025, 11, 12), On(2026, 11, 13), On(2024, 1, 1)], Today);

        graph.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    [InlineData(40, 4)]
    public void A_week_is_shaded_in_fixed_steps(int count, int level)
    {
        ProfileActivityGraph.Level(count).Should().Be(level);
    }

    [Fact]
    public void The_text_alternative_lists_each_month_with_how_much_happened()
    {
        var graph = ProfileActivityGraph.Build([On(2026, 3, 2), On(2026, 3, 20), On(2026, 11, 1)], Today);

        graph.Months.First().Label.Should().Be("Nov 2025");
        graph.Months.Last().Label.Should().Be("Nov 2026");
        graph.Months.Single(m => m.Label == "Mar 2026").Count.Should().Be(2);
        graph.Months.Single(m => m.Label == "Nov 2026").Count.Should().Be(1);
        graph.Months.Single(m => m.Label == "Apr 2026").Count.Should().Be(0);
    }

    [Fact]
    public void The_week_a_month_starts_in_carries_its_three_letter_name()
    {
        var graph = ProfileActivityGraph.Build([], Today);

        // 1 June 2026 is a Monday; 1 March 2026 is a Sunday, so it falls in the week starting 23 February.
        graph.Weeks.Single(w => w.Start == new DateOnly(2026, 6, 1)).MonthStart.Should().Be("Jun");
        graph.Weeks.Single(w => w.Start == new DateOnly(2026, 2, 23)).MonthStart.Should().Be("Mar");
        graph.Weeks.Single(w => w.Start == new DateOnly(2026, 3, 2)).MonthStart.Should().BeNull();
        graph.Weeks.Count(w => w.MonthStart != null).Should().Be(12);
    }

    [Fact]
    public void Without_activity_the_graph_is_empty()
    {
        ProfileActivityGraph.Build([], Today).IsEmpty.Should().BeTrue();
        ProfileActivityGraph.Build([On(2026, 10, 1)], Today).IsEmpty.Should().BeFalse();
    }
}
