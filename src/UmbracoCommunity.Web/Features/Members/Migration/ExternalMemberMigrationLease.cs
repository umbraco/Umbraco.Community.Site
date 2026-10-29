using System.Globalization;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>
/// A cross-instance "one run at a time" guard for <see cref="ExternalMemberMigrationService.MigrateAsync"/>,
/// stored as a time-limited claim in Umbraco's key/value table.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors how Umbraco's own <c>DistributedJobService</c> claims a job: a short transaction takes
/// the database-level distributed write lock (<see cref="Umbraco.Cms.Core.Constants.Locks.KeyValues"/>), checks the claim
/// and writes a new one, then commits. The lock itself is not held while the migration runs, on purpose:
/// Umbraco's distributed locks live as long as the ambient scope's transaction, so holding one for the
/// whole run would turn every per-member transaction into a nested scope (one failed member would roll
/// back all of them) and, on SQLite, would block every other write for the duration.
/// </para>
/// <para>
/// The claim expires after <see cref="Duration"/> so a crashed instance can't block the migration forever.
/// </para>
/// </remarks>
public sealed class ExternalMemberMigrationLease
{
    internal const string Key = "UmbracoCommunity.ExternalMemberMigration.Lease";

    /// <summary>How long a claim is honoured. Far longer than a run is expected to take.</summary>
    internal static readonly TimeSpan Duration = TimeSpan.FromHours(1);

    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IKeyValueService _keyValueService;
    private readonly TimeProvider _timeProvider;

    public ExternalMemberMigrationLease(ICoreScopeProvider scopeProvider, IKeyValueService keyValueService, TimeProvider timeProvider)
    {
        _scopeProvider = scopeProvider;
        _keyValueService = keyValueService;
        _timeProvider = timeProvider;
    }

    /// <summary>Claims the lease. Returns the claim to pass to <see cref="Release"/>, or null when another run holds it.</summary>
    public string? TryAcquire()
    {
        var now = _timeProvider.GetUtcNow();
        var claim = $"{Environment.MachineName}:{Guid.NewGuid():N}|{(now + Duration).ToString("O", CultureInfo.InvariantCulture)}";

        using var scope = _scopeProvider.CreateCoreScope();
        scope.EagerWriteLock(Umbraco.Cms.Core.Constants.Locks.KeyValues);

        if (IsHeld(_keyValueService.GetValue(Key), now))
        {
            scope.Complete();
            return null;
        }

        // Joins this scope's transaction, so the read above and this write are atomic under the lock.
        _keyValueService.SetValue(Key, claim);
        scope.Complete();
        return claim;
    }

    /// <summary>Releases the lease, but only if it is still <paramref name="claim"/> (it may have expired and been re-claimed).</summary>
    public void Release(string claim) => _keyValueService.TrySetValue(Key, claim, string.Empty);

    /// <summary>True when <paramref name="value"/> is a claim that hasn't expired yet. Unreadable values count as free.</summary>
    internal static bool IsHeld(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var separator = value.LastIndexOf('|');
        return separator >= 0
            && DateTimeOffset.TryParse(value[(separator + 1)..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires)
            && expires > now;
    }
}
