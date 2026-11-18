using System.Globalization;
using System.Text.Json.Nodes;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// Builds the Open Badges 2.0 JSON-LD documents (Issuer profile, BadgeClass and hosted Assertion) for the MVP badges.
/// Every <c>id</c> is the absolute URL the document is served from, under <paramref name="baseUri"/> (the site's scheme
/// and host), which is what hosted verification checks.
/// </summary>
public static class OpenBadgeDocuments
{
    public const string Context = "https://w3id.org/openbadges/v2";

    public static JsonObject Issuer(Uri baseUri, OpenBadgesOptions options)
    {
        var issuer = new JsonObject
        {
            ["@context"] = Context,
            ["type"] = "Issuer",
            ["id"] = OpenBadgesRoutes.IssuerUrl(baseUri),
            ["name"] = options.IssuerName,
            ["url"] = string.IsNullOrWhiteSpace(options.IssuerUrl) ? baseUri.ToString() : options.IssuerUrl,
        };

        if (!string.IsNullOrWhiteSpace(options.IssuerEmail))
        {
            issuer["email"] = options.IssuerEmail;
        }

        return issuer;
    }

    public static JsonObject MvpBadgeClass(Uri baseUri, int year, OpenBadgesOptions options)
    {
        var criteria = new JsonObject
        {
            ["narrative"] = $"Named an Umbraco MVP for {year}, in recognition of outstanding contributions to the Umbraco community.",
        };

        if (!string.IsNullOrWhiteSpace(options.CriteriaUrl))
        {
            criteria["id"] = options.CriteriaUrl;
        }

        return new JsonObject
        {
            ["@context"] = Context,
            ["type"] = "BadgeClass",
            ["id"] = OpenBadgesRoutes.MvpBadgeClassUrl(baseUri, year),
            ["name"] = $"Umbraco MVP {year}",
            ["description"] = $"Awarded to the members of the Umbraco community who were named an Umbraco MVP for {year}.",
            ["image"] = OpenBadgesRoutes.MvpImageUrl(baseUri, year),
            ["criteria"] = criteria,
            ["issuer"] = OpenBadgesRoutes.IssuerUrl(baseUri),
            ["tags"] = new JsonArray("Umbraco", "MVP", year.ToString(CultureInfo.InvariantCulture)),
        };
    }

    /// <param name="assertionId">The id made by <see cref="MvpBadgeTokenService.Create"/>.</param>
    /// <param name="recipientUrl">The member's public profile page, the identity the badge is issued to.</param>
    public static JsonObject MvpAssertion(Uri baseUri, string assertionId, int year, string recipientUrl, OpenBadgesOptions options) =>
        new()
        {
            ["@context"] = Context,
            ["type"] = "Assertion",
            ["id"] = OpenBadgesRoutes.AssertionUrl(baseUri, assertionId),
            ["recipient"] = new JsonObject
            {
                ["type"] = "url",
                ["hashed"] = false,
                ["identity"] = recipientUrl,
            },
            ["badge"] = OpenBadgesRoutes.MvpBadgeClassUrl(baseUri, year),
            ["verification"] = new JsonObject { ["type"] = "HostedBadge" },
            ["issuedOn"] = FormatDate(IssuedOn(year)),
            ["expires"] = FormatDate(Expires(year, options.ExpiryGraceMonths)),
        };

    /// <summary>
    /// When an MVP badge counts as issued: the start of the MVP year, 1 June. Fixed rather than the download time, so
    /// the hosted assertion (built again on every request, with no database behind it) never changes.
    /// </summary>
    public static DateTimeOffset IssuedOn(int year) => new(year, 6, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// When an MVP badge expires: the MVP year runs June to June, so the end of the year is 1 June of the next year
    /// (00:00 UTC), moved on by <paramref name="graceMonths"/> months as a buffer (one month by default: 1 July).
    /// </summary>
    public static DateTimeOffset Expires(int year, int graceMonths) =>
        new DateTimeOffset(year + 1, 6, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(Math.Max(0, graceMonths));

    /// <summary>ISO 8601 with a time zone indicator, as Open Badges 2.0 requires.</summary>
    public static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
