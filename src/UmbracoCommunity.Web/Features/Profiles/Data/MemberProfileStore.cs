using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

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
                OnboardingStartedUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
            };
            context.MemberProfiles.Add(entity);
            await context.SaveChangesAsync(ct);
        }

        return entity;
    }

    public async Task UpdateBioAsync(Guid memberKey, string? bio, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (entity == null) return;

        entity.Bio = bio;
        entity.UpdatedUtc = DateTime.UtcNow;
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
            .OrderBy(f => f.AddedUtc)
            .ToListAsync(ct);
    }

    public async Task<MemberFeedEntity?> AddFeedAsync(Guid memberKey, string platform, string url, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var profile = await context.MemberProfiles.FirstOrDefaultAsync(p => p.MemberKey == memberKey, ct);
        if (profile == null) return null;

        var feed = new MemberFeedEntity
        {
            MemberProfileId = profile.Id,
            Platform = platform,
            Url = url,
            AddedUtc = DateTime.UtcNow,
        };
        context.MemberFeeds.Add(feed);
        await context.SaveChangesAsync(ct);
        return feed;
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
