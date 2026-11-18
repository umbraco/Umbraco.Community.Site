using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using UmbracoCommunity.Web.Features.OpenBadges.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.OpenBadges;

public class MvpBadgeServiceTests
{
    private const string Handle = "octocat";
    private const string ProfileUrl = "https://community.example/profile/octocat";
    private static readonly Uri BaseUri = OpenBadgesTestData.BaseUri;

    private readonly Mock<IMvpBadgeEligibility> _eligibility = new();
    private readonly Mock<IOpenBadgeRecipientResolver> _recipients = new();

    public MvpBadgeServiceTests()
    {
        _eligibility.Setup(e => e.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MvpStatus.None);
        _recipients.Setup(r => r.GetProfileUrl(BaseUri, Handle)).Returns(ProfileUrl);
    }

    [Fact]
    public async Task A_current_mvp_can_download_the_badge_of_their_current_year()
    {
        GivenStatus(Handle, new MvpStatus([2026, 2025], IsCurrent: true));

        (await Service().GetDownloadableYearAsync(Handle)).Should().Be(2026);
    }

    [Fact]
    public async Task A_past_mvp_cannot_download_a_badge()
    {
        GivenStatus(Handle, new MvpStatus([2025], IsCurrent: false));
        var service = Service();

        (await service.GetDownloadableYearAsync(Handle)).Should().BeNull();
        (await service.BakeAsync(BaseUri, Handle, 2025)).Should().BeNull();
    }

    [Fact]
    public async Task No_badge_is_offered_for_a_year_without_artwork()
    {
        GivenStatus(Handle, new MvpStatus([1999], IsCurrent: true));

        (await Service().GetDownloadableYearAsync(Handle)).Should().BeNull();
    }

    [Fact]
    public async Task The_assertion_of_an_mvp_resolves()
    {
        GivenStatus(Handle, new MvpStatus([2026], IsCurrent: true));
        var id = OpenBadgesTestData.Tokens().Create(Handle, 2026);

        var assertion = await Service().GetAssertionAsync(BaseUri, id);

        assertion.Should().NotBeNull();
        assertion!["id"]!.GetValue<string>().Should().Be($"https://community.example/badges/assertions/{id}.json");
        assertion["recipient"]!["identity"]!.GetValue<string>().Should().Be(ProfileUrl);
    }

    [Fact]
    public async Task The_assertion_of_someone_who_was_not_an_mvp_that_year_does_not_resolve()
    {
        GivenStatus(Handle, new MvpStatus([2025], IsCurrent: false));
        var id = OpenBadgesTestData.Tokens().Create(Handle, 2026);

        (await Service().GetAssertionAsync(BaseUri, id)).Should().BeNull();
    }

    [Fact]
    public async Task The_assertion_of_a_non_mvp_does_not_resolve()
    {
        var id = OpenBadgesTestData.Tokens().Create("someone", 2026);

        (await Service().GetAssertionAsync(BaseUri, id)).Should().BeNull();
    }

    [Fact]
    public async Task A_past_year_still_resolves_but_has_expired()
    {
        GivenStatus(Handle, new MvpStatus([2027, 2026], IsCurrent: true));
        var id = OpenBadgesTestData.Tokens().Create(Handle, 2026);

        var assertion = await Service().GetAssertionAsync(BaseUri, id);

        assertion.Should().NotBeNull();
        DateTimeOffset.Parse(assertion!["expires"]!.GetValue<string>()).Should().BeBefore(new DateTimeOffset(2027, 7, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task A_made_up_id_does_not_resolve_and_does_not_look_anyone_up()
    {
        (await Service().GetAssertionAsync(BaseUri, "b2N0b2NhdDoyMDI2AAAAAAAAAAAAAAAAAAAAAA")).Should().BeNull();

        _eligibility.Verify(e => e.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Without_a_profile_page_no_badge_is_issued()
    {
        GivenStatus(Handle, new MvpStatus([2026], IsCurrent: true));
        _recipients.Setup(r => r.GetProfileUrl(BaseUri, Handle)).Returns((string?)null);
        var service = Service();

        (await service.GetAssertionAsync(BaseUri, OpenBadgesTestData.Tokens().Create(Handle, 2026))).Should().BeNull();
        (await service.BakeAsync(BaseUri, Handle, 2026)).Should().BeNull();
    }

    [Fact]
    public async Task The_download_bakes_the_same_assertion_the_hosted_url_serves()
    {
        GivenStatus(Handle, new MvpStatus([2026], IsCurrent: true));
        var service = Service();

        var baked = await service.BakeAsync(BaseUri, Handle, 2026);

        baked.Should().NotBeNull();
        var bakedAssertion = JsonNode.Parse(BakedText(baked!))!.AsObject();
        var id = bakedAssertion["id"]!.GetValue<string>();
        var token = id["https://community.example/badges/assertions/".Length..^".json".Length];
        var hosted = await service.GetAssertionAsync(BaseUri, token);
        JsonNode.DeepEquals(bakedAssertion, hosted).Should().BeTrue();
    }

    [Fact]
    public async Task The_download_can_bake_just_the_assertion_url()
    {
        GivenStatus(Handle, new MvpStatus([2026], IsCurrent: true));

        var baked = await Service(new OpenBadgesOptions { SigningKey = OpenBadgesTestData.SigningKey, BakeAssertionUrl = true })
            .BakeAsync(BaseUri, Handle, 2026);

        BakedText(baked!).Should().StartWith("https://community.example/badges/assertions/").And.EndWith(".json");
    }

    [Fact]
    public async Task Only_the_current_year_can_be_downloaded()
    {
        GivenStatus(Handle, new MvpStatus([2026], IsCurrent: true));

        (await Service().BakeAsync(BaseUri, Handle, 2025)).Should().BeNull();
    }

    [Fact]
    public async Task Without_a_signing_key_everything_is_off()
    {
        GivenStatus(Handle, new MvpStatus([2026], IsCurrent: true));
        var service = Service(new OpenBadgesOptions());

        service.IsEnabled.Should().BeFalse();
        service.GetIssuer(BaseUri).Should().BeNull();
        service.GetBadgeClass(BaseUri, 2026).Should().BeNull();
        service.GetImage(2026).Should().BeNull();
        (await service.GetDownloadableYearAsync(Handle)).Should().BeNull();
    }

    [Fact]
    public void Badge_classes_exist_only_for_years_with_artwork()
    {
        var service = Service();

        service.GetBadgeClass(BaseUri, 2026).Should().NotBeNull();
        service.GetBadgeClass(BaseUri, 1999).Should().BeNull();
        service.GetImage(1999).Should().BeNull();
    }

    private void GivenStatus(string handle, MvpStatus status) =>
        _eligibility.Setup(e => e.GetStatusAsync(handle, It.IsAny<CancellationToken>())).ReturnsAsync(status);

    private MvpBadgeService Service(OpenBadgesOptions? options = null)
    {
        options ??= OpenBadgesTestData.Options();
        return new MvpBadgeService(
            OpenBadgesTestData.Tokens(options.SigningKey),
            new MvpBadgeArtwork(Options.Create(options)),
            _eligibility.Object,
            _recipients.Object,
            Options.Create(options));
    }

    private static string BakedText(byte[] png)
    {
        var chunk = PngBadgeBaker.ReadChunks(png).Single(c => c.Type == "iTXt");
        var data = png.AsSpan(chunk.DataOffset, chunk.Length);
        return Encoding.UTF8.GetString(data[("openbadges".Length + 5)..]);
    }
}
