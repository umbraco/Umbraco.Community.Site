using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;
using UmbracoCommunity.Web.Features.OpenBadges.Controllers;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.OpenBadges;

public class OpenBadgeDocumentsTests
{
    private static readonly Uri BaseUri = OpenBadgesTestData.BaseUri;

    [Fact]
    public void The_issuer_has_the_required_properties()
    {
        var issuer = OpenBadgeDocuments.Issuer(BaseUri, new OpenBadgesOptions());

        Text(issuer, "@context").Should().Be("https://w3id.org/openbadges/v2");
        Text(issuer, "type").Should().Be("Issuer");
        Text(issuer, "id").Should().Be("https://community.example/badges/issuer.json");
        Text(issuer, "name").Should().Be("Umbraco Community");
        Text(issuer, "url").Should().Be("https://community.example/");
        issuer.ContainsKey("email").Should().BeFalse("no address is configured, so none is made up");
    }

    [Fact]
    public void The_issuer_uses_the_configured_url_and_email()
    {
        var issuer = OpenBadgeDocuments.Issuer(BaseUri, new OpenBadgesOptions { IssuerUrl = "https://umbraco.example", IssuerEmail = "badges@umbraco.example" });

        Text(issuer, "url").Should().Be("https://umbraco.example");
        Text(issuer, "email").Should().Be("badges@umbraco.example");
    }

    [Fact]
    public void The_badge_class_has_the_required_properties()
    {
        var badge = OpenBadgeDocuments.MvpBadgeClass(BaseUri, 2026, new OpenBadgesOptions());

        Text(badge, "@context").Should().Be("https://w3id.org/openbadges/v2");
        Text(badge, "type").Should().Be("BadgeClass");
        Text(badge, "id").Should().Be("https://community.example/badges/mvp/2026.json");
        Text(badge, "name").Should().Be("Umbraco MVP 2026");
        Text(badge, "description").Should().NotBeNullOrWhiteSpace();
        Text(badge, "image").Should().Be("https://community.example/badges/mvp/2026.png");
        Text(badge["criteria"]!.AsObject(), "narrative").Should().NotBeNullOrWhiteSpace();
        Text(badge, "issuer").Should().Be("https://community.example/badges/issuer.json");
    }

    [Fact]
    public void The_assertion_is_hosted_and_issued_to_the_profile_url()
    {
        var assertion = OpenBadgeDocuments.MvpAssertion(BaseUri, "abc", 2026, "https://community.example/profile/octocat", new OpenBadgesOptions());

        Text(assertion, "@context").Should().Be("https://w3id.org/openbadges/v2");
        Text(assertion, "type").Should().Be("Assertion");
        Text(assertion, "id").Should().Be("https://community.example/badges/assertions/abc.json");
        Text(assertion, "badge").Should().Be("https://community.example/badges/mvp/2026.json");

        var recipient = assertion["recipient"]!.AsObject();
        Text(recipient, "type").Should().Be("url");
        recipient["hashed"]!.GetValue<bool>().Should().BeFalse();
        Text(recipient, "identity").Should().Be("https://community.example/profile/octocat");

        Text(assertion["verification"]!.AsObject(), "type").Should().Be("HostedBadge");
        Text(assertion, "issuedOn").Should().Be("2026-06-01T00:00:00Z");
        Text(assertion, "expires").Should().Be("2027-07-01T00:00:00Z");
    }

    [Fact]
    public void The_assertion_id_shares_the_issuer_origin_so_hosted_verification_passes()
    {
        var assertion = OpenBadgeDocuments.MvpAssertion(BaseUri, "abc", 2026, "https://community.example/profile/octocat", new OpenBadgesOptions());
        var issuer = OpenBadgeDocuments.Issuer(BaseUri, new OpenBadgesOptions());

        new Uri(Text(assertion, "id")!).GetLeftPart(UriPartial.Authority)
            .Should().Be(new Uri(Text(issuer, "id")!).GetLeftPart(UriPartial.Authority));
    }

    [Theory]
    [InlineData(2026, 0, "2027-06-01T00:00:00Z")]
    [InlineData(2026, 1, "2027-07-01T00:00:00Z")]
    [InlineData(2027, 2, "2028-08-01T00:00:00Z")]
    [InlineData(2026, -5, "2027-06-01T00:00:00Z")]
    public void A_badge_expires_at_the_end_of_the_june_to_june_mvp_year_plus_the_grace_months(int year, int graceMonths, string expected) =>
        OpenBadgeDocuments.FormatDate(OpenBadgeDocuments.Expires(year, graceMonths)).Should().Be(expected);

    [Theory]
    [InlineData(null, "application/ld+json")]
    [InlineData("*/*", "application/ld+json")]
    [InlineData("application/ld+json", "application/ld+json")]
    [InlineData("application/ld+json, application/json;q=0.9", "application/ld+json")]
    [InlineData("application/json", "application/json")]
    public void Json_ld_is_served_unless_only_plain_json_is_accepted(string? accept, string expected) =>
        OpenBadgesController.PreferredJsonContentType(accept).Should().Be(expected);

    private static string? Text(JsonObject json, string property) => json[property]?.GetValue<string>();
}
