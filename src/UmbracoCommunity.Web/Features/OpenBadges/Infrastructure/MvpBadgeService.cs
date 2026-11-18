using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// Issues the Open Badges 2.0 "Umbraco MVP" badges: one BadgeClass per MVP year that has artwork, and per member a
/// hosted Assertion whose id (see <see cref="MvpBadgeTokenService"/>) resolves back to their handle and year. Only a
/// current MVP can download a baked badge; the assertion of a past year still resolves, but has expired.
/// Everything returns null when the feature is off (no signing key) or the badge isn't issuable.
/// </summary>
public sealed class MvpBadgeService(
    MvpBadgeTokenService tokens,
    MvpBadgeArtwork artwork,
    IMvpBadgeEligibility eligibility,
    IOpenBadgeRecipientResolver recipients,
    IOptions<OpenBadgesOptions> options)
{
    private readonly OpenBadgesOptions _options = options.Value;

    public bool IsEnabled => tokens.IsConfigured;

    public bool IsIssuable(int year) => IsEnabled && artwork.Exists(year);

    public JsonObject? GetIssuer(Uri baseUri) => IsEnabled ? OpenBadgeDocuments.Issuer(baseUri, _options) : null;

    public JsonObject? GetBadgeClass(Uri baseUri, int year) =>
        IsIssuable(year) ? OpenBadgeDocuments.MvpBadgeClass(baseUri, year, _options) : null;

    public byte[]? GetImage(int year) => IsIssuable(year) ? artwork.GetPng(year) : null;

    /// <summary>The year whose badge <paramref name="handle"/> can download: their current MVP year, when it has artwork.</summary>
    public async Task<int?> GetDownloadableYearAsync(string? handle, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var year = (await eligibility.GetStatusAsync(handle, cancellationToken)).CurrentYear;
        return year is { } y && IsIssuable(y) ? y : null;
    }

    /// <summary>
    /// The hosted assertion for <paramref name="id"/>, or null when the id is not one we made, or its handle was not an
    /// MVP in its year. A past year resolves (with <c>expires</c> in the past) so verifiers show it as expired.
    /// </summary>
    public async Task<JsonObject?> GetAssertionAsync(Uri baseUri, string id, CancellationToken cancellationToken = default)
    {
        if (!tokens.TryRead(id, out var handle, out var year) || !IsIssuable(year))
        {
            return null;
        }

        var status = await eligibility.GetStatusAsync(handle, cancellationToken);
        if (!status.Years.Contains(year))
        {
            return null;
        }

        var recipientUrl = recipients.GetProfileUrl(baseUri, handle);
        return recipientUrl is null ? null : OpenBadgeDocuments.MvpAssertion(baseUri, id, year, recipientUrl, _options);
    }

    /// <summary>
    /// The <paramref name="year"/> badge image of <paramref name="handle"/> with their assertion baked in, or null
    /// unless that is their current MVP year.
    /// </summary>
    public async Task<byte[]?> BakeAsync(Uri baseUri, string? handle, int year, CancellationToken cancellationToken = default)
    {
        if (handle is null || await GetDownloadableYearAsync(handle, cancellationToken) != year)
        {
            return null;
        }

        var recipientUrl = recipients.GetProfileUrl(baseUri, handle);
        var image = artwork.GetPng(year);
        if (recipientUrl is null || image is null)
        {
            return null;
        }

        // Open Badges 2.0 baking stores the assertion JSON itself; its id is the hosted URL a verifier re-fetches.
        var assertion = OpenBadgeDocuments.MvpAssertion(baseUri, tokens.Create(handle, year), year, recipientUrl, _options);
        var baked = _options.BakeAssertionUrl ? assertion["id"]!.GetValue<string>() : assertion.ToJsonString();
        return PngBadgeBaker.Bake(image, baked);
    }
}
