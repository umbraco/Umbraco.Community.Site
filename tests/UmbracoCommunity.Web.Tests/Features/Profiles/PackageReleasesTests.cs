using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class PackageReleasesTests
{
    [Theory]
    [InlineData("https://www.nuget.org/packages/Cultiv.Hangfire/5.5.2", "cultiv.hangfire", "5.5.2")]
    [InlineData("https://www.nuget.org/packages/Cultiv.Hangfire", "cultiv.hangfire", null)]
    public void A_nuget_address_names_the_package_and_version(string url, string id, string? version)
    {
        PackageReleases.IdOf(url).Should().Be(id);
        PackageReleases.VersionOf(url).Should().Be(version);
    }

    [Theory]
    [InlineData("https://example.com/packages/Cultiv.Hangfire/5.5.2")]
    [InlineData("https://www.nuget.org/profiles/warrenbuckley")]
    [InlineData("not a url")]
    public void Anything_else_names_no_package(string url) => PackageReleases.IdOf(url).Should().BeNull();

    [Fact]
    public void Versions_match_ignoring_case_and_build_metadata()
    {
        PackageReleases.SameVersion("1.0.0-Alpha+abc", "1.0.0-alpha").Should().BeTrue();
        PackageReleases.SameVersion("1.0.1", "1.0.0").Should().BeFalse();
    }

    [Fact]
    public void A_timeline_entry_is_a_routine_release_unless_it_is_a_new_package()
    {
        var release = new ProfileTimelineEntry(PlatformActivityGroups.Packages, "a", "Released X v1.0.1", null, null, DateTimeOffset.UtcNow, null, null);

        release.IsRoutineRelease.Should().BeTrue();
        ProfileTimeline.IsShown(release, null).Should().BeFalse();
        ProfileTimeline.IsShown(release with { IsNewPackage = true }, null).Should().BeTrue();
        ProfileTimeline.IsShown(release, PlatformActivityGroups.Packages).Should().BeTrue();
    }
}
