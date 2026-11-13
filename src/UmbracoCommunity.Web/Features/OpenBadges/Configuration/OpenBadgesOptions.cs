namespace UmbracoCommunity.Web.Features.OpenBadges.Configuration;

/// <summary>
/// Settings for the Open Badges 2.0 "Umbraco MVP" badges (section <c>OpenBadges</c>).
/// See <c>docs/tutorials/foundations/open-badges-mvp.md</c>.
/// </summary>
public sealed class OpenBadgesOptions
{
    public const string SectionName = "OpenBadges";

    /// <summary>The shortest <see cref="SigningKey"/> that is accepted.</summary>
    public const int MinSigningKeyLength = 32;

    /// <summary>
    /// Secret that signs the assertion ids, so an id can't be made up for someone who isn't an MVP. A secret: set it in
    /// appsettings.Local.json or the environment, never commit it. At least <see cref="MinSigningKeyLength"/> characters.
    /// When it is missing (or too short) every badge endpoint returns 404 and the account page shows no badge.
    /// Changing it breaks every badge already downloaded (its assertion URL stops resolving).
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>The issuer name shown by badge verifiers.</summary>
    public string IssuerName { get; set; } = "Umbraco Community";

    /// <summary>
    /// The issuer's contact address. The Open Badges 2.0 spec lists <c>email</c> as expected on an issuer profile,
    /// but it is left out of the profile until one is configured here.
    /// </summary>
    public string? IssuerEmail { get; set; }

    /// <summary>
    /// The issuer's website. Empty means the site the badge is served from (scheme and host of the request).
    /// </summary>
    public string? IssuerUrl { get; set; }

    /// <summary>
    /// A page explaining what it takes to become an MVP, used as the badge criteria link. Optional: the criteria
    /// always carry a short narrative.
    /// </summary>
    public string? CriteriaUrl { get; set; }

    /// <summary>
    /// How many months after the end of the MVP year (1 June of the next year, 00:00 UTC) a badge expires. The default
    /// of 1 gives a buffer until 1 July, for when the next MVPs are announced a little after the year turns over;
    /// 0 expires it exactly at the end of the MVP year.
    /// </summary>
    public int ExpiryGraceMonths { get; set; } = 1;

    /// <summary>
    /// What goes in the PNG's <c>openbadges</c> chunk. False (the default) bakes the assertion JSON, as the Open Badges
    /// 2.0 Baking Specification describes; true bakes only the hosted assertion URL, the older style some
    /// consumers still read. Either way verifiers re-fetch the hosted assertion.
    /// </summary>
    public bool BakeAssertionUrl { get; set; }

    /// <summary>Width and height, in pixels, of the badge images (the artwork is square). 0 keeps the artwork's own size.</summary>
    public int ImageSize { get; set; } = 400;
}
