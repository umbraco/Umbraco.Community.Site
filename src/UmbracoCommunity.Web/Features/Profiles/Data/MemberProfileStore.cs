using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>
/// Data access layer for member profiles and feeds. Mirrors <c>BlockRestrictionStore</c>'s
/// shape (a new <see cref="MemberProfilesDbContext"/> per call via <see cref="IDbContextFactory{TContext}"/>).
/// No caching: this data isn't on a hot per-page-render path (unlike block restrictions),
/// so reads always reflect the latest write.
/// </summary>
public class MemberProfileStore
{
    private readonly IDbContextFactory<MemberProfilesDbContext> _contextFactory;

    public MemberProfileStore(IDbContextFactory<MemberProfilesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<MemberProfileEntity?> GetByMemberKeyAsync(Guid memberKey, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.MemberProfiles
            .Include(p => p.Feeds)
            .FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
    }

    /// <summary>Looks up a profile by its cached GitHub handle — used by the public profile provider.</summary>
    public async Task<MemberProfileEntity?> GetByHandleAsync(string handle, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.MemberProfiles
            .Include(p => p.Feeds)
            .FirstOrDefaultAsync(p => p.GitHubHandle.ToLower() == handle.ToLower(), ct);
    }

    /// <summary>Idempotently creates (on first call) or fetches the member's profile row, marking onboarding in progress.</summary>
    public async Task<MemberProfileEntity> GetOrStartOnboardingAsync(Guid memberKey, string gitHubHandle, string displayName, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new MemberProfileEntity
            {
                MemberKey = memberKey,
                GitHubHandle = gitHubHandle,
                DisplayName = displayName,
                OnboardingStatus = OnboardingStatus.InProgress,
                // New profiles start hidden until the member chooses to show them.
                IsProfileVisible = false,
                OnboardingStartedUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
            };
            context.MemberProfiles.Add(entity);
            await context.SaveChangesAsync(ct);
        }

        return entity;
    }

    /// <summary>
    /// Sets the member's own bio. Empty or whitespace-only text is stored as null, so the
    /// GitHub bio shows again. Returns the updated row, or null when the member has no profile row.
    /// </summary>
    public async Task<MemberProfileEntity?> UpdateBioAsync(Guid memberKey, string? bio, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return null;

        entity.Bio = ProfileText.Normalize(bio);
        entity.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Sets the member's own headline. Empty or whitespace-only text is stored as null, so the
    /// GitHub company shows again.
    /// Returns the updated row, or null when the member has no profile row.
    /// </summary>
    public async Task<MemberProfileEntity?> UpdateHeadlineAsync(Guid memberKey, string? headline, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return null;

        entity.Headline = ProfileText.Normalize(headline);
        entity.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Sets the member's own display name. Empty or whitespace-only text is stored as null, so
    /// the GitHub name shows again. Returns the updated row, or null when the member has no profile row.
    /// </summary>
    public async Task<MemberProfileEntity?> UpdateDisplayNameAsync(Guid memberKey, string? displayName, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return null;

        entity.DisplayNameOverride = ProfileText.Normalize(displayName);
        entity.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>Stores the bio and company fetched from the member's GitHub profile and when they were fetched.</summary>
    public async Task SetGitHubDefaultsAsync(Guid memberKey, string? gitHubBio, string? gitHubCompany, DateTime fetchedUtc, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return;

        entity.GitHubBio = gitHubBio;
        entity.GitHubCompany = gitHubCompany;
        entity.GitHubFetchedUtc = fetchedUtc;
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAvatarAsync(Guid memberKey, Guid? avatarMediaKey, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return;

        entity.AvatarMediaKey = avatarMediaKey;
        entity.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Sets whether the member's profile is visible to everyone. Returns false when the member has no profile row.</summary>
    public async Task<bool> SetProfileVisibleAsync(Guid memberKey, bool isVisible, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return false;

        entity.IsProfileVisible = isVisible;
        entity.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MemberProfileEntity?> CompleteOnboardingAsync(Guid memberKey, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return null;

        var now = DateTime.UtcNow;
        entity.OnboardingStatus = OnboardingStatus.Completed;
        entity.OnboardingCompletedUtc = now;
        entity.UpdatedUtc = now;
        await context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<MemberFeedEntity>> GetActiveFeedsAsync(Guid memberKey, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.MemberFeeds
            .Where(f => f.MemberProfile!.MemberKey == memberKey && !f.IsRemoved)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.AddedUtc)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);
    }

    public async Task<MemberFeedEntity?> AddFeedAsync(Guid memberKey, string platform, string url, LinkPreview? preview = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var profile = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (profile == null) return null;

        var highestOrder = await context.MemberFeeds
            .Where(f => f.MemberProfileId == profile.Id && !f.IsRemoved)
            .MaxAsync(f => (int?)f.SortOrder, ct) ?? 0;

        var feed = new MemberFeedEntity
        {
            MemberProfileId = profile.Id,
            Platform = platform,
            Url = url,
            AddedUtc = DateTime.UtcNow,
            SortOrder = highestOrder + 1,
        };
        ApplyPreview(feed, preview);
        context.MemberFeeds.Add(feed);
        await context.SaveChangesAsync(ct);
        return feed;
    }

    /// <summary>Changes a link's platform name and address. A new address resets its sync state and replaces its preview.</summary>
    public async Task<MemberFeedEntity?> UpdateFeedAsync(Guid memberKey, int feedId, string platform, string url, LinkPreview? preview, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var feed = await context.MemberFeeds
            .FirstOrDefaultAsync(f => f.Id == feedId && f.MemberProfile!.MemberKey == memberKey && !f.IsRemoved, ct);
        if (feed == null) return null;

        var urlChanged = !string.Equals(feed.Url, url, StringComparison.Ordinal);
        feed.Platform = platform;
        feed.Url = url;
        if (urlChanged)
        {
            feed.PlatformSyncStatus = FeedSyncStatus.Pending;
            feed.LastPlatformSyncAttemptUtc = null;
            feed.LastPlatformSyncError = null;
            ApplyPreview(feed, preview);
        }

        await context.SaveChangesAsync(ct);
        return feed;
    }

    /// <summary>
    /// Stores the order of the member's active links. <paramref name="orderedIds"/> must name each of them exactly once.
    /// </summary>
    public async Task<bool> ReorderFeedsAsync(Guid memberKey, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var feeds = await context.MemberFeeds
            .Where(f => f.MemberProfile!.MemberKey == memberKey && !f.IsRemoved)
            .ToListAsync(ct);

        if (orderedIds.Count != feeds.Count || orderedIds.Distinct().Count() != feeds.Count || feeds.Any(f => !orderedIds.Contains(f.Id)))
        {
            return false;
        }

        for (var position = 0; position < orderedIds.Count; position++)
        {
            feeds.First(f => f.Id == orderedIds[position]).SortOrder = position + 1;
        }

        await context.SaveChangesAsync(ct);
        return true;
    }

    private static void ApplyPreview(MemberFeedEntity feed, LinkPreview? preview)
    {
        feed.PreviewStatus = preview?.Status ?? LinkPreviewStatus.Unchecked;
        feed.PreviewTitle = preview?.Title;
        feed.PreviewImageUrl = preview?.ImageUrl;
        feed.FaviconUrl = preview?.FaviconUrl;
        feed.PreviewCheckedUtc = preview == null ? null : DateTime.UtcNow;
    }

    public async Task<bool> SetFeedHiddenAsync(Guid memberKey, int feedId, bool isHidden, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var feed = await context.MemberFeeds
            .FirstOrDefaultAsync(f => f.Id == feedId && f.MemberProfile!.MemberKey == memberKey && !f.IsRemoved, ct);
        if (feed == null) return false;

        feed.IsHidden = isHidden;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveFeedAsync(Guid memberKey, int feedId, string? reason, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var feed = await context.MemberFeeds
            .FirstOrDefaultAsync(f => f.Id == feedId && f.MemberProfile!.MemberKey == memberKey && !f.IsRemoved, ct);
        if (feed == null) return false;

        feed.IsRemoved = true;
        feed.RemovedReason = reason;
        feed.RemovedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task MarkFeedPlatformSyncResultAsync(int feedId, bool succeeded, string? error, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var feed = await context.MemberFeeds.FirstOrDefaultAsync(f => f.Id == feedId, ct);
        if (feed == null) return;

        feed.PlatformSyncStatus = succeeded ? FeedSyncStatus.Synced : FeedSyncStatus.Failed;
        feed.LastPlatformSyncAttemptUtc = DateTime.UtcNow;
        feed.LastPlatformSyncError = succeeded ? null : error;
        await context.SaveChangesAsync(ct);
    }
}
