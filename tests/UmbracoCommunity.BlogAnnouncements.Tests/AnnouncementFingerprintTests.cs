using FluentAssertions;
using UmbracoCommunity.BlogAnnouncements.Detection;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class AnnouncementFingerprintTests
{
    private static readonly DateTimeOffset Morning = new(2026, 6, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Evening = new(2026, 6, 11, 20, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextDay = new(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SameAuthorTitleAndDay_ProduceSameFingerprint()
    {
        var a = AnnouncementFingerprint.Compute("Jane Dev", "My Post", Morning);
        var b = AnnouncementFingerprint.Compute("Jane Dev", "My Post", Evening);
        a.Should().Be(b);
    }

    [Fact]
    public void CaseAndWhitespaceAreNormalized()
    {
        var a = AnnouncementFingerprint.Compute("Jane   Dev", "My  Post", Morning);
        var b = AnnouncementFingerprint.Compute("jane dev", "my post", Morning);
        a.Should().Be(b);
    }

    [Fact]
    public void DifferentDay_ProducesDifferentFingerprint()
    {
        var a = AnnouncementFingerprint.Compute("Jane Dev", "My Post", Morning);
        var b = AnnouncementFingerprint.Compute("Jane Dev", "My Post", NextDay);
        a.Should().NotBe(b);
    }

    [Fact]
    public void DifferentTitle_ProducesDifferentFingerprint()
    {
        var a = AnnouncementFingerprint.Compute("Jane Dev", "My Post", Morning);
        var b = AnnouncementFingerprint.Compute("Jane Dev", "Other Post", Morning);
        a.Should().NotBe(b);
    }
}
