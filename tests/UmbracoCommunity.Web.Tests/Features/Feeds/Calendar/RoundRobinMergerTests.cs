using FluentAssertions;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

public class RoundRobinMergerTests
{
    [Fact]
    public void Single_feed_returns_items_in_order_capped_at_max()
    {
        var feed = new[] { "a1", "a2", "a3", "a4" };
        var result = RoundRobinMerger.Merge(new[] { feed }, maxItems: 3);
        result.Should().Equal("a1", "a2", "a3");
    }

    [Fact]
    public void Two_feeds_interleave_round_robin()
    {
        var a = new[] { "a1", "a2", "a3" };
        var b = new[] { "b1", "b2", "b3" };
        var result = RoundRobinMerger.Merge(new[] { a, b }, maxItems: 6);
        result.Should().Equal("a1", "b1", "a2", "b2", "a3", "b3");
    }

    [Fact]
    public void Exhausted_feed_is_skipped()
    {
        var a = new[] { "a1" };
        var b = new[] { "b1", "b2", "b3" };
        var result = RoundRobinMerger.Merge(new[] { a, b }, maxItems: 4);
        result.Should().Equal("a1", "b1", "b2", "b3");
    }

    [Fact]
    public void Stops_at_max_items_even_if_more_available()
    {
        var a = new[] { "a1", "a2", "a3" };
        var b = new[] { "b1", "b2", "b3" };
        var result = RoundRobinMerger.Merge(new[] { a, b }, maxItems: 3);
        result.Should().Equal("a1", "b1", "a2");
    }

    [Fact]
    public void Empty_feeds_collection_returns_empty()
    {
        var result = RoundRobinMerger.Merge(Array.Empty<string[]>(), maxItems: 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void All_feeds_empty_returns_empty()
    {
        var result = RoundRobinMerger.Merge(new[] { Array.Empty<string>(), Array.Empty<string>() }, maxItems: 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Zero_max_items_returns_empty()
    {
        var a = new[] { "a1", "a2" };
        var result = RoundRobinMerger.Merge(new[] { a }, maxItems: 0);
        result.Should().BeEmpty();
    }
}
