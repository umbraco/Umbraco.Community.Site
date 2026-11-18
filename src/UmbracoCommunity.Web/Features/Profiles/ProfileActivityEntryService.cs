using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The activity members add to their profile by hand: what they add, edit and withdraw, what a moderator removes (or
/// shows after all), and the shown entries the profile page shows. There is no check before an entry shows: it is on
/// the profile as soon as it is added, and a moderator can remove it afterwards. Shown entries are cached per handle and
/// the cache is cleared whenever one is added, edited, removed, shown or withdrawn. The platform is told about entries
/// that start or stop showing, and about edits to shown ones; if that fails the change still counts.
/// </summary>
/// <remarks>
/// An entry's picture (see <see cref="ProfileActivityImageStore"/>) is stored before the entry is saved, so the entry
/// never points at a file that isn't there; when the entry then can't be added or edited the new picture is deleted
/// again. A picture an edit replaces or removes is deleted only once the edit is saved. Withdrawing an entry deletes its
/// picture (the entry stops pointing at it first). A removed entry keeps its picture, since a moderator can still change
/// their mind and show it; it goes when the member withdraws the entry.
/// </remarks>
public sealed class ProfileActivityEntryService
{
    private const string CacheKeyPrefix = "ProfileActivityEntry:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly MemberActivityEntryStore _store;
    private readonly IProfileSyncClient _syncClient;
    private readonly ProfileActivityImageStore _imageStore;
    private readonly AppCaches _appCaches;
    private readonly ILogger<ProfileActivityEntryService> _logger;

    public ProfileActivityEntryService(
        MemberActivityEntryStore store,
        IProfileSyncClient syncClient,
        ProfileActivityImageStore imageStore,
        AppCaches appCaches,
        ILogger<ProfileActivityEntryService> logger)
    {
        _store = store;
        _syncClient = syncClient;
        _imageStore = imageStore;
        _appCaches = appCaches;
        _logger = logger;
    }

    /// <summary>
    /// Adds an entry, on the profile at once, with the picture already stored at <paramref name="imagePath"/> (from
    /// <see cref="ProfileActivityImageStore.SaveAsync"/>), if any. When it can't be added (see
    /// <see cref="MemberActivityEntryStore.AddAsync"/>) the picture is deleted again, as it is when adding fails.
    /// </summary>
    public async Task<ActivityEntryChange> AddAsync(
        int memberProfileId,
        AddActivityEntryRequest entry,
        string? imagePath = null,
        CancellationToken ct = default)
    {
        ActivityEntryChange? change = null;
        try
        {
            change = await _store.AddAsync(memberProfileId, entry, imagePath, ct);
        }
        finally
        {
            if (change?.Saved != true && imagePath != null)
            {
                _imageStore.Delete(imagePath);
            }
        }

        // Set when the store returned; an exception from it has already left this method.
        if (change!.Entry is { State: ActivityEntryState.Verified } added)
        {
            Forget(added.GitHubHandle);
            await NotifyAsync(() => _syncClient.NotifyActivityVerifiedAsync(added.GitHubHandle, added, ct), added.GitHubHandle);
        }

        return change;
    }

    /// <summary>
    /// Edits one of the member's own entries (see <see cref="MemberActivityEntryStore.UpdateAsync"/>). A new picture in
    /// <paramref name="image"/> is already stored; it is deleted again when the edit isn't saved. The picture it replaces,
    /// or one removed, is deleted once the edit is saved. For a shown entry the cache is cleared and the platform told:
    /// when the link changed, the old one is removed and the new one announced; otherwise it is announced again with the
    /// new title, description and date.
    /// </summary>
    public async Task<ActivityEntryChange> UpdateAsync(
        int memberProfileId,
        int entryId,
        AddActivityEntryRequest entry,
        ActivityEntryImageChange image,
        CancellationToken ct = default)
    {
        (ActivityEntryChange Change, ActivityEntryRecord? Before)? result = null;
        try
        {
            result = await _store.UpdateAsync(memberProfileId, entryId, entry, image, ct);
        }
        finally
        {
            if (result?.Change.Saved != true && image.NewPath != null)
            {
                _imageStore.Delete(image.NewPath);
            }
        }

        var (change, before) = result!.Value;
        if (!change.Saved || before == null)
        {
            return change;
        }

        var updated = change.Entry!;
        if (before.ImagePath != null && before.ImagePath != updated.ImagePath)
        {
            _imageStore.Delete(before.ImagePath);
        }

        if (updated.State == ActivityEntryState.Verified)
        {
            Forget(updated.GitHubHandle);
            if (!string.Equals(before.Url, updated.Url, StringComparison.Ordinal))
            {
                await NotifyAsync(() => _syncClient.NotifyActivityRemovedAsync(updated.GitHubHandle, before.Url, ct), updated.GitHubHandle);
            }

            await NotifyAsync(() => _syncClient.NotifyActivityVerifiedAsync(updated.GitHubHandle, updated, ct), updated.GitHubHandle);
        }

        return change;
    }

    /// <summary>The address of the entry's picture, on this site, or null when it has none.</summary>
    public string? ImageUrl(ActivityEntryRecord entry) =>
        entry.ImagePath == null ? null : _imageStore.Url(entry.ImagePath);

    public Task<IReadOnlyList<ActivityEntryRecord>> ListForMemberAsync(int memberProfileId, CancellationToken ct = default) =>
        _store.ListForMemberAsync(memberProfileId, ct);

    public Task<ActivityEntryPage> ListAsync(ActivityEntryState state, int skip, int take, CancellationToken ct = default) =>
        _store.ListAsync(state, skip, take, ct);

    /// <summary>The entries shown on the profile with the given handle, newest first.</summary>
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

        if (entry.ImagePath != null)
        {
            _imageStore.Delete(entry.ImagePath);
        }

        if (wasVerified)
        {
            Forget(entry.GitHubHandle);
            await NotifyAsync(
                () => _syncClient.NotifyActivityRemovedAsync(entry.GitHubHandle, entry.Url, ct),
                entry.GitHubHandle);
        }

        return entry with { ImagePath = null };
    }

    /// <summary>
    /// Shows an entry: a legacy waiting one, or restores one a moderator removed. Returns <c>null</c> when there is no
    /// such entry or it is already shown or withdrawn.
    /// </summary>
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
    /// Removes a shown (or legacy waiting) entry with a note for the member; one that was shown stops showing at once.
    /// Returns <c>null</c> when there is no such entry or it is already removed or withdrawn.
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

    // Wraps the list so "nothing shown" is cached too.
    private sealed record CachedEntries(IReadOnlyList<ActivityEntryRecord> Entries);
}
