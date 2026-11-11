using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The activity members add to their profile by hand: what they submit and withdraw, what an admin verifies or
/// rejects, and the verified entries the profile page shows. Verified entries are cached per handle and the
/// cache is cleared whenever one is verified or withdrawn. The platform is told about verified and withdrawn
/// entries; if that fails the entry still counts.
/// </summary>
public sealed class ProfileActivityEntryService
{
    private const string CacheKeyPrefix = "ProfileActivityEntry:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly MemberActivityEntryStore _store;
    private readonly IProfileSyncClient _syncClient;
    private readonly AppCaches _appCaches;
    private readonly ILogger<ProfileActivityEntryService> _logger;

    public ProfileActivityEntryService(
        MemberActivityEntryStore store,
        IProfileSyncClient syncClient,
        AppCaches appCaches,
        ILogger<ProfileActivityEntryService> logger)
    {
        _store = store;
        _syncClient = syncClient;
        _appCaches = appCaches;
        _logger = logger;
    }

    public Task<ActivityEntryRecord?> AddAsync(int memberProfileId, AddActivityEntryRequest entry, CancellationToken ct = default) =>
        _store.AddAsync(memberProfileId, entry, ct);

    public Task<IReadOnlyList<ActivityEntryRecord>> ListForMemberAsync(int memberProfileId, CancellationToken ct = default) =>
        _store.ListForMemberAsync(memberProfileId, ct);

    public Task<ActivityEntryPage> ListAsync(ActivityEntryState state, int skip, int take, CancellationToken ct = default) =>
        _store.ListAsync(state, skip, take, ct);

    /// <summary>The verified entries shown on the profile with the given handle, newest first.</summary>
    public async Task<IReadOnlyList<ActivityEntryRecord>> GetVerifiedAsync(string handle, CancellationToken ct = default)
    {
        var key = CacheKeyPrefix + handle.ToLowerInvariant();
        if (_appCaches.RuntimeCache.Get(key) is CachedEntries cached)
        {
            return cached.Entries;
        }

        var entries = new CachedEntries(await _store.ListVerifiedAsync(handle, ct));
        _appCaches.RuntimeCache.Insert(key, () => entries, CacheDuration);
        return entries.Entries;
    }

    public async Task<ActivityEntryRecord?> WithdrawAsync(int memberProfileId, int entryId, CancellationToken ct = default)
    {
        var result = await _store.WithdrawAsync(memberProfileId, entryId, ct);
        if (result is not var (entry, wasVerified))
        {
            return null;
        }

        if (wasVerified)
        {
            Forget(entry.GitHubHandle);
            await NotifyAsync(
                () => _syncClient.NotifyActivityRemovedAsync(entry.GitHubHandle, entry.Url, ct),
                entry.GitHubHandle);
        }

        return entry;
    }

    /// <summary>Verifies a waiting or rejected entry. Returns <c>null</c> when there is no such entry or it cannot be verified.</summary>
    public async Task<ActivityEntryRecord?> VerifyAsync(int entryId, Guid? userKey, string? userName, CancellationToken ct = default)
    {
        var result = await _store.DecideAsync(entryId, verify: true, rejectionReason: null, userKey, userName, ct);
        if (result is not var (entry, _))
        {
            return null;
        }

        Forget(entry.GitHubHandle);
        await NotifyAsync(() => _syncClient.NotifyActivityVerifiedAsync(entry.GitHubHandle, entry, ct), entry.GitHubHandle);
        return entry;
    }

    /// <summary>
    /// Rejects a waiting or verified entry with a note for the member; one that was shown stops showing at once.
    /// Returns <c>null</c> when there is no such entry or it cannot be rejected.
    /// </summary>
    public async Task<ActivityEntryRecord?> RejectAsync(int entryId, string reason, Guid? userKey, string? userName, CancellationToken ct = default)
    {
        var result = await _store.DecideAsync(entryId, verify: false, reason, userKey, userName, ct);
        if (result is not var (entry, wasVerified))
        {
            return null;
        }

        if (wasVerified)
        {
            Forget(entry.GitHubHandle);
            await NotifyAsync(() => _syncClient.NotifyActivityRemovedAsync(entry.GitHubHandle, entry.Url, ct), entry.GitHubHandle);
        }

        return entry;
    }

    private void Forget(string handle) =>
        _appCaches.RuntimeCache.Clear(CacheKeyPrefix + handle.ToLowerInvariant());

    private async Task NotifyAsync(Func<Task> notify, string handle)
    {
        try
        {
            await notify();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not tell the content platform about an activity entry change for {Handle}", handle);
        }
    }

    // Wraps the list so "nothing verified" is cached too.
    private sealed record CachedEntries(IReadOnlyList<ActivityEntryRecord> Entries);
}
