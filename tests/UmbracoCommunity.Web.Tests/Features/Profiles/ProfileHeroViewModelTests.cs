using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileHeroViewModelTests
{
    private static readonly ProfileIdentity Identity = new(
        "The Octocat", null, null, null, null, null, false, [], "octocat", null, [], false);

    [Fact]
    public void Has_no_badges_unless_given_some()
    {
        new ProfileHeroViewModel { Identity = Identity }.Badges.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Gold", "Gold forum badge", "gold")]
    [InlineData("Silver", "Silver forum badge", "silver")]
    [InlineData("bronze", "bronze forum badge", "bronze")]
    [InlineData(null, "Forum badge", null)]
    [InlineData("Platinum", "Platinum forum badge", null)]
    public void Puts_the_kind_of_badge_in_words_and_only_known_kinds_in_a_class(string? type, string kind, string? modifier)
    {
        var badge = new ProfileBadge("Umbraco MVP", null, null, null, type);

        ProfileHeroViewModel.BadgeKind(badge).Should().Be(kind);
        ProfileHeroViewModel.BadgeTypeModifier(badge).Should().Be(modifier);
    }

    [Fact]
    public void The_tooltip_has_the_kind_and_the_description()
    {
        ProfileHeroViewModel.BadgeTitle(new ProfileBadge("Umbraco MVP", null, "An Umbraco MVP", null, "Gold"))
            .Should().Be("Gold forum badge: An Umbraco MVP");
        ProfileHeroViewModel.BadgeTitle(new ProfileBadge("Member", null, null, null, null))
            .Should().Be("Forum badge");
    }
}
