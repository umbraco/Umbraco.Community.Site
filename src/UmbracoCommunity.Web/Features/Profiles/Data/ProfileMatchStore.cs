using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Matching;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>
/// Stores match decisions (see <see cref="ProfileMatchEntity"/>). A new <see cref="MemberProfilesDbContext"/> is
/// created per call, like <see cref="MemberProfileStore"/>.
/// </summary>
public class ProfileMatchStore
{
    private const int MaxSourceUrlLength = ProfileMatchEntity.MaxSourceUrlLength;
    private const int MaxSignalsLength = 4000;

    private readonly IDbContextFactory<MemberProfilesDbContext> _contextFactory;

    public ProfileMatchStore(IDbContextFactory<MemberProfilesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Records an automatic decision, replacing an earlier automatic one. A decision made by a person (a backoffice
    /// user or the member) is never replaced: the stored record is returned as it is. Returns <c>null</c> when the
    /// profile does not exist.
    /// </summary>
    public async Task<ProfileMatchRecord?> RecordAutomaticAsync(
        int memberProfileId,
        string sourceUrl,
        ProfileMatchState state,
        double confidence,
        IReadOnlyList<MatchSignal> signals,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var profile = await context.MemberProfiles.FirstOrDefaultAsync(p => p.Id == memberProfileId, ct);
        if (profile == null)
        {
            return null;
        }

        ValidateSourceUrl(sourceUrl);

        var url = sourceUrl;
        var entity = await context.ProfileMatches
            .FirstOrDefaultAsync(m => m.MemberProfileId == memberProfileId && m.SourceUrl == url, ct);

        if (entity != null && entity.DecidedBy != ProfileMatchDecider.Automatic)
        {
            return ToRecord(entity, profile);
        }

        var now = DateTime.UtcNow;
        if (entity == null)
        {
            entity = new ProfileMatchEntity
            {
                MemberProfileId = memberProfileId,
                SourceUrl = url,
                CreatedUtc = now,
            };
            context.ProfileMatches.Add(entity);
        }

        entity.State = state;
        entity.Confidence = Math.Clamp(confidence, 0, 1);
        entity.SignalsJson = SerializeSignals(signals);
        entity.DecidedBy = ProfileMatchDecider.Automatic;
        entity.DecidedByUserKey = null;
        entity.DecidedByUserName = null;
        entity.DecidedUtc = now;

        await context.SaveChangesAsync(ct);
        return ToRecord(entity, profile);
    }

    /// <summary>
    /// Records that the member says <paramref name="sourceUrl"/> is not theirs: the match becomes
    /// <see cref="ProfileMatchState.Rejected"/>, decided by <see cref="ProfileMatchDecider.Member"/>, and is created when
    /// there was none. The signals are added to those already stored. Returns <c>null</c> when the profile does not exist.
    /// </summary>
    public async Task<ProfileMatchRecord?> RecordMemberRejectionAsync(
        int memberProfileId,
        string sourceUrl,
        IReadOnlyList<MatchSignal> signals,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var profile = await context.MemberProfiles.FirstOrDefaultAsync(p => p.Id == memberProfileId, ct);
        if (profile == null)
        {
            return null;
        }

        ValidateSourceUrl(sourceUrl);

        var url = sourceUrl;
        var entity = await context.ProfileMatches
            .FirstOrDefaultAsync(m => m.MemberProfileId == memberProfileId && m.SourceUrl == url, ct);

        var now = DateTime.UtcNow;
        if (entity == null)
        {
            entity = new ProfileMatchEntity
            {
                MemberProfileId = memberProfileId,
                SourceUrl = url,
                CreatedUtc = now,
            };
            context.ProfileMatches.Add(entity);
        }

        entity.State = ProfileMatchState.Rejected;
        entity.SignalsJson = SerializeSignals(DeserializeSignals(entity.SignalsJson).Concat(signals).Distinct().ToList());
        entity.DecidedBy = ProfileMatchDecider.Member;
        entity.DecidedByUserKey = null;
        entity.DecidedByUserName = null;
        entity.DecidedUtc = now;

        await context.SaveChangesAsync(ct);
        return ToRecord(entity, profile);
    }

    /// <summary>The sources rejected for the profile with the given GitHub handle (ignoring case); empty when there is no such profile.</summary>
    public async Task<IReadOnlyList<string>> GetRejectedSourcesAsync(string gitHubHandle, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var handle = gitHubHandle.ToLower();
        return await context.ProfileMatches
            .AsNoTracking()
            .Where(m => m.State == ProfileMatchState.Rejected && m.MemberProfile!.GitHubHandle.ToLower() == handle)
            .Select(m => m.SourceUrl)
            .ToListAsync(ct);
    }

    /// <summary>Every stored match of one profile, by source.</summary>
    public async Task<IReadOnlyDictionary<string, ProfileMatchRecord>> GetForProfileAsync(int memberProfileId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rows = await context.ProfileMatches
            .AsNoTracking()
            .Include(m => m.MemberProfile)
            .Where(m => m.MemberProfileId == memberProfileId)
            .ToListAsync(ct);

        return rows
            .Select(m => ToRecord(m, m.MemberProfile!))
            .ToDictionary(r => r.SourceUrl, StringComparer.Ordinal);
    }

    /// <summary>
    /// Records a person's decision (<see cref="ProfileMatchState.Confirmed"/> or <see cref="ProfileMatchState.Rejected"/>).
    /// Returns <c>null</c> when the match does not exist.
    /// </summary>
    public async Task<ProfileMatchRecord?> DecideManuallyAsync(
        int matchId,
        ProfileMatchState state,
        Guid? userKey,
        string? userName,
        CancellationToken ct = default)
    {
        if (state == ProfileMatchState.Held)
        {
            throw new ArgumentException("A manual decision must confirm or reject the match.", nameof(state));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.ProfileMatches
            .Include(m => m.MemberProfile)
            .FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (entity == null)
        {
            return null;
        }

        entity.State = state;
        entity.DecidedBy = ProfileMatchDecider.Manual;
        entity.DecidedByUserKey = userKey;
        entity.DecidedByUserName = Truncate(userName, 200);
        entity.DecidedUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        return ToRecord(entity, entity.MemberProfile!);
    }

    /// <summary>Matches in one state, newest decision first.</summary>
    public async Task<ProfileMatchPage> ListAsync(ProfileMatchState state, int skip, int take, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var query = context.ProfileMatches
            .AsNoTracking()
            .Include(m => m.MemberProfile)
            .Where(m => m.State == state);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(m => m.DecidedUtc)
            .ThenByDescending(m => m.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return new ProfileMatchPage(rows.Select(m => ToRecord(m, m.MemberProfile!)).ToList(), total);
    }

    private static void ValidateSourceUrl(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl) || sourceUrl.Length > MaxSourceUrlLength)
        {
            throw new ArgumentException($"The source must be 1 to {MaxSourceUrlLength} characters.", nameof(sourceUrl));
        }
    }

    private static ProfileMatchRecord ToRecord(ProfileMatchEntity entity, MemberProfileEntity profile) =>
        new(
            entity.Id,
            profile.GitHubHandle,
            profile.DisplayNameOverride ?? profile.DisplayName,
            entity.SourceUrl,
            entity.State,
            entity.Confidence,
            DeserializeSignals(entity.SignalsJson),
            entity.DecidedBy,
            entity.DecidedByUserName,
            entity.DecidedUtc);

    private static string SerializeSignals(IReadOnlyList<MatchSignal> signals)
    {
        var kept = signals.ToList();
        var json = JsonSerializer.Serialize(kept);
        while (json.Length > MaxSignalsLength && kept.Count > 0)
        {
            kept.RemoveAt(kept.Count - 1);
            json = JsonSerializer.Serialize(kept);
        }

        return json;
    }

    private static IReadOnlyList<MatchSignal> DeserializeSignals(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<MatchSignal>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];
}
