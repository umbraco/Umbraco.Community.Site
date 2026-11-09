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
    /// Adds an entry that waits for a check. Returns <c>null</c> when the member already has
    /// <see cref="ProfileActivityEntry.MaxPendingEntries"/> waiting, or the same link is already waiting or verified.
    /// Expects values from <see cref="ProfileActivityEntry.TryNormalize"/>.
    /// </summary>
    public async Task<ActivityEntryRecord?> AddAsync(int memberProfileId, AddActivityEntryRequest entry, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var profile = await context.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == memberProfileId, ct);
        if (profile == null)
        {
            return null;
        }

        var open = await context.MemberActivityEntries
            .AsNoTracking()
            .Where(e => e.MemberProfileId == memberProfileId
                && (e.State == ActivityEntryState.Pending || e.State == ActivityEntryState.Verified))
            .Select(e => new { e.State, e.Url })
            .ToListAsync(ct);
        if (open.Count(e => e.State == ActivityEntryState.Pending) >= ProfileActivityEntry.MaxPendingEntries
            || open.Any(e => string.Equals(e.Url, entry.Url, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var entity = new MemberActivityEntryEntity
        {
            MemberProfileId = memberProfileId,
            Kind = entry.Kind!,
            Title = entry.Title!,
            Url = entry.Url!,
            DateUtc = entry.Date!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Description = entry.Description,
            State = ActivityEntryState.Pending,
            CreatedUtc = DateTime.UtcNow,
        };
        context.MemberActivityEntries.Add(entity);
        await context.SaveChangesAsync(ct);
        return ToRecord(entity, profile);
    }

    /// <summary>The member's entries that are waiting, verified or rejected, newest first.</summary>
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
    /// Withdraws one of the member's own entries, whether it is waiting or already shown; <c>WasVerified</c> says
    /// which. Returns <c>null</c> when the member has no such entry that can be withdrawn.
    /// </summary>
    public async Task<(ActivityEntryRecord Entry, bool WasVerified)?> WithdrawAsync(int memberProfileId, int entryId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.MemberActivityEntries
            .Include(e => e.MemberProfile)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MemberProfileId == memberProfileId, ct);
        if (entity == null || entity.State is not (ActivityEntryState.Pending or ActivityEntryState.Verified))
        {
            return null;
        }

        var wasVerified = entity.State == ActivityEntryState.Verified;
        entity.State = ActivityEntryState.Withdrawn;
        await context.SaveChangesAsync(ct);
        return (ToRecord(entity, entity.MemberProfile!), wasVerified);
    }

    /// <summary>
    /// Records an admin's decision on an entry: verified, or rejected with a note for the member. An admin can
    /// change their mind either way; <c>WasVerified</c> says whether the entry was shown before. Returns <c>null</c>
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
            entity.CreatedUtc);
}
