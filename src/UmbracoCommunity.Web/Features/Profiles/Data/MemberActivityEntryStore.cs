using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>
/// Stores the activity members add to their profile by hand (see <see cref="MemberActivityEntryEntity"/>). A new
/// <see cref="MemberProfilesDbContext"/> is created per call, like <see cref="MemberProfileStore"/>.
/// </summary>
public class MemberActivityEntryStore
{
    private readonly IDbContextFactory<MemberProfilesDbContext> _contextFactory;

    public MemberActivityEntryStore(IDbContextFactory<MemberProfilesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Adds an entry, shown on the profile at once (<see cref="ActivityEntryState.Verified"/>). Refused when the member
    /// already has an entry with the same link (or had one a moderator removed). There is deliberately no cap on how
    /// many entries a member adds; moderators remove what doesn't belong. Expects values from
    /// <see cref="ProfileActivityEntry.TryNormalize"/>, and <paramref name="imagePath"/> from
    /// <see cref="ProfileActivityImageStore.SaveAsync"/>.
    /// </summary>
    public async Task<ActivityEntryChange> AddAsync(
        int memberProfileId,
        AddActivityEntryRequest entry,
        string? imagePath = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var profile = await context.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == memberProfileId, ct);
        if (profile == null)
        {
            return new ActivityEntryChange(ActivityEntryChangeOutcome.NotFound);
        }

        if (await LinkConflictAsync(context, memberProfileId, entry.Url!, exceptEntryId: null, ct) is { } conflict)
        {
            return new ActivityEntryChange(conflict);
        }

        var entity = new MemberActivityEntryEntity
        {
            MemberProfileId = memberProfileId,
            Kind = entry.Kind!,
            Title = entry.Title!,
            Url = entry.Url!,
            DateUtc = ToDateUtc(entry.Date!.Value),
            Description = entry.Description,
            ImagePath = imagePath,
            State = ActivityEntryState.Verified,
            CreatedUtc = DateTime.UtcNow,
        };
        context.MemberActivityEntries.Add(entity);
        await context.SaveChangesAsync(ct);
        return new ActivityEntryChange(ActivityEntryChangeOutcome.Saved, ToRecord(entity, profile));
    }

    /// <summary>
    /// Changes one of the member's own entries to <paramref name="entry"/> (values from <see cref="ProfileActivityEntry.TryNormalize"/>)
    /// and its picture as <paramref name="image"/> says. The entry keeps its id and its state: one on the profile stays
    /// on it, a legacy waiting one keeps waiting. One a moderator removed can't be edited
    /// (<see cref="ActivityEntryChangeOutcome.RemovedByModerator"/>); a link another of the member's entries has, or had
    /// before a moderator removed it, is refused as when adding. A pin of the entry has its stored copy of the item
    /// refreshed in the same save (see <see cref="ProfileDisplay.TryTakeSnapshots"/>). Returns the entry as it was
    /// (<c>Before</c>, whose <see cref="ActivityEntryRecord.ImagePath"/> names the picture to delete when it was replaced
    /// or removed) and as it is now. No file is touched here.
    /// </summary>
    public async Task<(ActivityEntryChange Change, ActivityEntryRecord? Before)> UpdateAsync(
        int memberProfileId,
        int entryId,
        AddActivityEntryRequest entry,
        ActivityEntryImageChange image,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberActivityEntries
            .Include(e => e.MemberProfile)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MemberProfileId == memberProfileId, ct);
        if (entity == null || entity.State == ActivityEntryState.Withdrawn)
        {
            return (new ActivityEntryChange(ActivityEntryChangeOutcome.NotFound), null);
        }

        if (entity.State == ActivityEntryState.Rejected)
        {
            return (new ActivityEntryChange(ActivityEntryChangeOutcome.RemovedByModerator), null);
        }

        if (await LinkConflictAsync(context, memberProfileId, entry.Url!, exceptEntryId: entryId, ct) is { } conflict)
        {
            return (new ActivityEntryChange(conflict), null);
        }

        var before = ToRecord(entity, entity.MemberProfile!);
        entity.Kind = entry.Kind!;
        entity.Title = entry.Title!;
        entity.Url = entry.Url!;
        entity.DateUtc = ToDateUtc(entry.Date!.Value);
        entity.Description = entry.Description;
        if (image.NewPath != null)
        {
            entity.ImagePath = image.NewPath;
        }
        else if (image.Remove)
        {
            entity.ImagePath = null;
        }

        var after = ToRecord(entity, entity.MemberProfile!);
        var activityId = ProfileActivityEntry.ActivityId(entryId);
        var pins = await context.MemberPinnedItems
            .Where(p => p.MemberProfileId == memberProfileId && p.ActivityId == activityId)
            .ToListAsync(ct);
        if (pins.Count > 0)
        {
            var item = ProfileActivityEntry.FeaturedItem(after);
            foreach (var pin in pins)
            {
                pin.Group = item.Group;
                pin.Title = item.Title;
                pin.Url = item.Url;
                pin.Description = item.Description;
                pin.DateUtc = item.Date?.UtcDateTime;
                pin.Source = item.Source;
            }
        }

        await context.SaveChangesAsync(ct);
        return (new ActivityEntryChange(ActivityEntryChangeOutcome.Saved, after), before);
    }

    /// <summary>The member's entries that are shown, (legacy) waiting or removed by a moderator, newest first.</summary>
    public async Task<IReadOnlyList<ActivityEntryRecord>> ListForMemberAsync(int memberProfileId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rows = await context.MemberActivityEntries
            .AsNoTracking()
            .Include(e => e.MemberProfile)
            .Where(e => e.MemberProfileId == memberProfileId && e.State != ActivityEntryState.Withdrawn)
            .OrderByDescending(e => e.CreatedUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        return rows.Select(e => ToRecord(e, e.MemberProfile!)).ToList();
    }

    /// <summary>Entries in one state, newest first.</summary>
    public async Task<ActivityEntryPage> ListAsync(ActivityEntryState state, int skip, int take, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var query = context.MemberActivityEntries
            .AsNoTracking()
            .Include(e => e.MemberProfile)
            .Where(e => e.State == state);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(e => e.CreatedUtc)
            .ThenByDescending(e => e.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return new ActivityEntryPage(rows.Select(e => ToRecord(e, e.MemberProfile!)).ToList(), total);
    }

    /// <summary>The verified entries of the profile with the given handle, newest first.</summary>
    public async Task<IReadOnlyList<ActivityEntryRecord>> ListVerifiedAsync(string handle, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rows = await context.MemberActivityEntries
            .AsNoTracking()
            .Include(e => e.MemberProfile)
            .Where(e => e.State == ActivityEntryState.Verified && e.MemberProfile!.GitHubHandle.ToLower() == handle.ToLower())
            .OrderByDescending(e => e.DateUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        return rows.Select(e => ToRecord(e, e.MemberProfile!)).ToList();
    }

    /// <summary>
    /// Withdraws one of the member's own entries, whether it is shown, (legacy) waiting or removed by a moderator;
    /// <c>WasVerified</c> says whether it was shown. The entry stops pointing at its picture; the returned record still
    /// names it (<see cref="ActivityEntryRecord.ImagePath"/>), so the caller can delete the file. A removed entry keeps
    /// the moderator's note, which is what keeps its link from being added again (see <see cref="AddAsync"/>). Returns
    /// <c>null</c> when the member has no such entry that can be withdrawn.
    /// </summary>
    public async Task<(ActivityEntryRecord Entry, bool WasVerified)?> WithdrawAsync(int memberProfileId, int entryId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberActivityEntries
            .Include(e => e.MemberProfile)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MemberProfileId == memberProfileId, ct);
        if (entity == null || entity.State == ActivityEntryState.Withdrawn)
        {
            return null;
        }

        var wasVerified = entity.State == ActivityEntryState.Verified;
        var imagePath = entity.ImagePath;
        entity.State = ActivityEntryState.Withdrawn;
        entity.ImagePath = null;
        await context.SaveChangesAsync(ct);
        return (ToRecord(entity, entity.MemberProfile!) with { ImagePath = imagePath }, wasVerified);
    }

    /// <summary>
    /// Records a moderator's decision on an entry: shown (a legacy waiting entry, or restoring a removed one), or
    /// removed with a note for the member. A moderator can change their mind either way; <c>WasVerified</c> says whether the entry was shown before. Returns <c>null</c>
    /// when there is no such entry, it was withdrawn, or it already has that decision.
    /// </summary>
    public async Task<(ActivityEntryRecord Entry, bool WasVerified)?> DecideAsync(
        int entryId,
        bool verify,
        string? rejectionReason,
        Guid? userKey,
        string? userName,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberActivityEntries
            .Include(e => e.MemberProfile)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entity == null || entity.State is ActivityEntryState.Withdrawn || entity.State == (verify ? ActivityEntryState.Verified : ActivityEntryState.Rejected))
        {
            return null;
        }

        var wasVerified = entity.State == ActivityEntryState.Verified;
        entity.State = verify ? ActivityEntryState.Verified : ActivityEntryState.Rejected;
        entity.RejectionReason = verify ? null : rejectionReason;
        entity.DecidedByUserKey = userKey;
        entity.DecidedByUserName = userName is { Length: > 200 } ? userName[..200] : userName;
        entity.DecidedUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        return (ToRecord(entity, entity.MemberProfile!), wasVerified);
    }

    /// <summary>
    /// Why the member can't have another entry with <paramref name="url"/>, or null when they can: another of their
    /// entries is shown or waiting with it (<see cref="ActivityEntryChangeOutcome.DuplicateLink"/>), or a moderator
    /// removed one with it (<see cref="ActivityEntryChangeOutcome.RemovedLink"/>), even if the member then withdrew that
    /// one: with entries shown at once, adding a removed link again must not put it straight back on the profile.
    /// Links match ignoring case, as <see cref="ProfileActivityService.AddVerifiedActivity"/> does.
    /// </summary>
    private static async Task<ActivityEntryChangeOutcome?> LinkConflictAsync(
        MemberProfilesDbContext context,
        int memberProfileId,
        string url,
        int? exceptEntryId,
        CancellationToken ct)
    {
        var others = await context.MemberActivityEntries
            .AsNoTracking()
            .Where(e => e.MemberProfileId == memberProfileId
                && (exceptEntryId == null || e.Id != exceptEntryId)
                && (e.State != ActivityEntryState.Withdrawn || e.RejectionReason != null))
            .Select(e => new { e.State, e.Url })
            .ToListAsync(ct);

        var sameLink = others.Where(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameLink.Any(e => e.State is ActivityEntryState.Rejected or ActivityEntryState.Withdrawn))
        {
            return ActivityEntryChangeOutcome.RemovedLink;
        }

        return sameLink.Count > 0 ? ActivityEntryChangeOutcome.DuplicateLink : null;
    }

    private static DateTime ToDateUtc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static ActivityEntryRecord ToRecord(MemberActivityEntryEntity entity, MemberProfileEntity profile) =>
        new(
            entity.Id,
            profile.GitHubHandle,
            profile.EffectiveDisplayName,
            entity.Kind,
            entity.Title,
            entity.Url,
            DateOnly.FromDateTime(entity.DateUtc),
            entity.Description,
            entity.State,
            entity.RejectionReason,
            entity.DecidedByUserName,
            entity.DecidedUtc,
            entity.CreatedUtc,
            entity.ImagePath);
}
