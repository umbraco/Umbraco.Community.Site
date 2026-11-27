namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// Whether the current request may see the enriched sections of a profile page.
/// </summary>
/// <param name="Show">True when the enriched sections may be shown.</param>
/// <param name="IsGated">
/// True when they are shown only because the viewer is in the early access group looking at
/// their own profile (the tenant's setting is off). Such a page must not be indexed.
/// </param>
public sealed record EnrichedSectionsAccess(bool Show, bool IsGated)
{
    public static readonly EnrichedSectionsAccess None = new(false, false);
}
