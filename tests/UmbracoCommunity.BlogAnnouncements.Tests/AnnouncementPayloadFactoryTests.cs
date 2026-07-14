using FluentAssertions;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class AnnouncementPayloadFactoryTests
{
    private const int Max = AnnouncementPayloadFactory.MaxExcerptLength;

    [Fact]
    public void TruncateExcerpt_NullOrShort_PassesThroughUnchanged()
    {
        AnnouncementPayloadFactory.TruncateExcerpt(null).Should().BeNull();
        AnnouncementPayloadFactory.TruncateExcerpt("").Should().Be("");
        AnnouncementPayloadFactory.TruncateExcerpt("A short excerpt.").Should().Be("A short excerpt.");
    }

    [Fact]
    public void TruncateExcerpt_ExactlyAtLimit_IsUnchanged()
    {
        var exact = new string('a', Max);
        AnnouncementPayloadFactory.TruncateExcerpt(exact).Should().Be(exact);
    }

    [Fact]
    public void TruncateExcerpt_LongText_CutsAtWordBoundaryWithEllipsis()
    {
        // Repeating words guarantee the cap lands mid-word somewhere past a boundary.
        var text = string.Join(" ", Enumerable.Repeat("wordhere", 60)); // 539 chars
        var result = AnnouncementPayloadFactory.TruncateExcerpt(text)!;

        result.Should().EndWith("…");
        result.Length.Should().BeLessThanOrEqualTo(Max + 1); // content ≤ cap, plus the ellipsis
        var withoutEllipsis = result[..^1];
        withoutEllipsis.Should().NotEndWith(" ");
        // No chopped token: every space-separated chunk must be a full "wordhere".
        withoutEllipsis.Split(' ').Should().OnlyContain(w => w == "wordhere");
    }

    [Fact]
    public void TruncateExcerpt_TrimsTrailingPunctuationBeforeEllipsis()
    {
        // Force the word boundary to land right after "sentence," so the comma would otherwise
        // render as ",…".
        var head = new string('a', Max - 12); // leaves room for " sentence, x…"
        var text = $"{head} sentence, extended well past the cap so truncation must kick in";
        var result = AnnouncementPayloadFactory.TruncateExcerpt(text)!;

        result.Should().EndWith("…");
        result.Should().NotContain(",…");
        result[..^1].Should().NotEndWith(",");
    }

    [Fact]
    public void TruncateExcerpt_SingleUnbrokenToken_HardCutsAtLimit()
    {
        // e.g. a giant URL with no whitespace — no boundary to back up to.
        var url = "https://example.com/" + new string('x', 500);
        var result = AnnouncementPayloadFactory.TruncateExcerpt(url)!;

        result.Should().EndWith("…");
        result.Length.Should().Be(Max + 1);
        result[..^1].Should().Be(url[..Max].TrimEnd('/'));
    }

    [Fact]
    public void FromPost_TruncatesLongExcerpt_AndLeavesShortOnesAlone()
    {
        var longPost = Post(excerpt: string.Join(" ", Enumerable.Repeat("paragraph", 100)));
        var shortPost = Post(excerpt: "A short excerpt.");

        AnnouncementPayloadFactory.FromPost(longPost).Excerpt!.Length.Should().BeLessThanOrEqualTo(Max + 1);
        AnnouncementPayloadFactory.FromPost(longPost).Excerpt.Should().EndWith("…");
        AnnouncementPayloadFactory.FromPost(shortPost).Excerpt.Should().Be("A short excerpt.");
    }

    [Fact]
    public void CreateTestMessage_ExcerptIsWithinCap()
    {
        var payload = AnnouncementPayloadFactory.CreateTestMessage(DateTimeOffset.UtcNow);
        payload.Excerpt!.Length.Should().BeLessThanOrEqualTo(Max);
    }

    private static AnnouncedBlogPost Post(string? excerpt) => new()
    {
        SphereId = Guid.NewGuid(),
        Url = "https://blog.example/a",
        Title = "A Post",
        PublishedAtUtc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
        Fingerprint = "fp",
        FirstSeenUtc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
        Excerpt = excerpt,
    };
}
