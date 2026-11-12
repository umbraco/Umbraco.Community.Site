using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>
/// Keeps what timeline entries' pages offered for a preview card (see <see cref="ProfileTimelinePreviewEntity"/>),
/// so a card survives its page changing or disappearing.
/// </summary>
public class ProfileTimelinePreviewStore
{
    private readonly IDbContextFactory<MemberProfilesDbContext> _contextFactory;

    public ProfileTimelinePreviewStore(IDbContextFactory<MemberProfilesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>The SHA-256 of an address, hex-encoded in lower case.</summary>
    public static string Hash(string url) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)));

    /// <summary>The kept previews for <paramref name="pageUrls"/>, by page address. Pages with nothing kept are left out.</summary>
    public async Task<IReadOnlyDictionary<string, ProfileTimelinePreviewEntity>> GetAsync(IEnumerable<string> pageUrls, CancellationToken ct = default)
    {
        var byHash = pageUrls.Distinct(StringComparer.Ordinal).ToDictionary(Hash, url => url, StringComparer.Ordinal);
        if (byHash.Count == 0)
        {
            return new Dictionary<string, ProfileTimelinePreviewEntity>();
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hashes = byHash.Keys.ToList();
        var rows = await context.ProfileTimelinePreviews.AsNoTracking().Where(p => hashes.Contains(p.PageUrlHash)).ToListAsync(ct);
        return rows
            .Where(r => string.Equals(byHash[r.PageUrlHash], r.PageUrl, StringComparison.Ordinal))
            .ToDictionary(r => r.PageUrl, StringComparer.Ordinal);
    }

    /// <summary>Keeps (or replaces) what the page at <paramref name="pageUrl"/> offered.</summary>
    public async Task SaveAsync(string pageUrl, string imageUrl, string? description, CancellationToken ct = default)
    {
        if (pageUrl.Length > ProfileTimelinePreviewEntity.MaxUrlLength || imageUrl.Length > ProfileTimelinePreviewEntity.MaxUrlLength)
        {
            return;
        }

        if (description is { Length: > ProfileTimelinePreviewEntity.MaxDescriptionLength })
        {
            description = description[..ProfileTimelinePreviewEntity.MaxDescriptionLength];
        }

        var hash = Hash(pageUrl);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var row = await context.ProfileTimelinePreviews.FirstOrDefaultAsync(p => p.PageUrlHash == hash, ct);
        if (row == null)
        {
            row = new ProfileTimelinePreviewEntity { PageUrlHash = hash, PageUrl = pageUrl };
            context.ProfileTimelinePreviews.Add(row);
        }

        row.ImageUrl = imageUrl;
        row.Description = description;
        row.CheckedUtc = DateTime.UtcNow;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request kept the same page at the same moment; its row is just as good.
        }
    }
}
