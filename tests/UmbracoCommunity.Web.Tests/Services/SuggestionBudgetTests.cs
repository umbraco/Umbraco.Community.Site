using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoCommunity.Web.Services;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Services;

public class SuggestionBudgetTests
{
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/141.0 Safari/537.36";

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static HttpContext Visitor(string ip = "203.0.113.5", string? userAgent = Browser)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (userAgent is not null) context.Request.Headers.UserAgent = userAgent;
        return context;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)")]
    [InlineData("curl/8.4.0")]
    public void TryConsume_RefusesBotsAndMissingUserAgents(string? userAgent)
    {
        using var budget = new SuggestionBudget(TimeProvider.System);

        budget.TryConsume(Visitor(userAgent: userAgent)).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_RefusesWithoutARequest()
    {
        using var budget = new SuggestionBudget(TimeProvider.System);

        budget.TryConsume(null).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_CapsEachVisitorPerHour()
    {
        var time = new FixedTime(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        using var budget = new SuggestionBudget(time);
        var allowed = 0;

        for (var i = 0; i < SuggestionBudget.PerVisitorPerHour + 5; i++)
        {
            time.Now = time.Now.AddMinutes(1);
            if (budget.TryConsume(Visitor())) allowed++;
        }

        allowed.Should().Be(SuggestionBudget.PerVisitorPerHour);
        budget.TryConsume(Visitor(ip: "198.51.100.7")).Should().BeTrue();
    }

    [Fact]
    public void TryConsume_CapsTheWholeSitePerMinute()
    {
        using var budget = new SuggestionBudget(new FixedTime(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero)));

        var allowed = Enumerable.Range(0, SuggestionBudget.SiteWidePerMinute + 10)
            .Count(i => budget.TryConsume(Visitor(ip: $"203.0.113.{i % 250}", userAgent: $"{Browser} {i}")));

        allowed.Should().Be(SuggestionBudget.SiteWidePerMinute);
    }
}
