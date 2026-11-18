using FluentAssertions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.OpenBadges;

public class MvpBadgeArtworkTests
{
    [Fact]
    public void The_2026_artwork_ships_and_is_resized_to_the_configured_size()
    {
        var artwork = new MvpBadgeArtwork(Options.Create(new OpenBadgesOptions { ImageSize = 400 }));

        artwork.Exists(2026).Should().BeTrue();
        var png = artwork.GetPng(2026);

        png.Should().NotBeNull();
        PngBadgeBaker.IsPng(png).Should().BeTrue();
        var info = Image.Identify(png);
        info.Width.Should().Be(400);
        info.Height.Should().Be(400);
    }

    [Fact]
    public void A_year_without_artwork_has_no_badge()
    {
        var artwork = new MvpBadgeArtwork(Options.Create(new OpenBadgesOptions()));

        artwork.Exists(1999).Should().BeFalse();
        artwork.GetPng(1999).Should().BeNull();
    }
}
