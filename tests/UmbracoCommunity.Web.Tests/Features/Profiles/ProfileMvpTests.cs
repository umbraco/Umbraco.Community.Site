using FluentAssertions;
using UmbracoCommunity.Web.Features.Mvp.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileMvpTests
{
    private const string Handle = "octocat";

    private static readonly ProfileIdentity Identity =
        new("The Octocat", null, null, null, null, null, false, [], Handle, null, [], false);

    private static MvpYear Year(int year, params string[] handles) =>
        new(year, handles.Select(h => new MvpMember(h, false, "https://example.dev/a.png", null, h)).ToList());

    [Fact]
    public void A_member_in_the_newest_year_is_a_current_mvp()
    {
        var result = ProfileMvp.Apply(Identity, null, [Year(2025, Handle), Year(2026, Handle, "someone")]);

        result.IsMvp.Should().BeTrue();
        result.IsCurrentMvp.Should().BeTrue();
        result.MvpYears.Should().Equal(2026, 2025);
    }

    [Fact]
    public void A_member_missing_from_the_newest_year_is_a_past_mvp()
    {
        var result = ProfileMvp.Apply(Identity, null, [Year(2024, Handle), Year(2026, "someone")]);

        result.IsMvp.Should().BeTrue();
        result.IsCurrentMvp.Should().BeFalse();
        result.MvpYears.Should().Equal(2024);
    }

    [Fact]
    public void A_member_who_was_never_an_mvp_is_left_alone()
    {
        var result = ProfileMvp.Apply(Identity, [], [Year(2026, "someone")]);

        result.Should().BeSameAs(Identity);
    }

    [Fact]
    public void Platform_years_win_over_the_list_and_can_be_newer_than_it()
    {
        var result = ProfileMvp.Apply(Identity, [2027, 2026, 2026], [Year(2026, Handle)]);

        result.MvpYears.Should().Equal(2027, 2026);
        result.IsCurrentMvp.Should().BeTrue();
    }

    [Fact]
    public void The_handle_is_matched_without_regard_to_case()
    {
        var result = ProfileMvp.Apply(Identity with { GitHubHandle = "OctoCat" }, null, [Year(2026, Handle)]);

        result.IsCurrentMvp.Should().BeTrue();
    }
}
