using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Infrastructure;

/// <summary>
/// Database-level delivery claims — the guard against announcing the same post twice.
///
/// Both delivery paths (the automatic cycle and the dashboard's manual repost) read a queue of
/// rows, POST to Discord, and only then write the outcome. Without a claim the row stays deliverable
/// in the database for the whole duration of the send, so any concurrent cycle — a second app
/// instance during a Cloud recycle, or a schedule that overlaps its predecessor — reads the same
/// row and sends the same message again. An in-process lock can't fix that: the racers are separate
/// processes.
///
/// So the winner is decided by the database instead: <see cref="TryClaimAsync"/> flips the row to
/// <see cref="AnnouncementStatus.Claimed"/> with a conditional <c>UPDATE</c> that is committed
/// <em>before</em> the first webhook call. The update is atomic, so exactly one caller sees a
/// row affected; every other caller sees zero and skips the post. <see cref="SettleAsync"/> then
/// writes the real outcome and clears the claim.
///
/// All writes here go through <c>ExecuteUpdateAsync</c> rather than the change tracker: the
/// condition has to be evaluated by the database, not against a snapshot this process read
/// earlier. Callers therefore load rows <c>AsNoTracking</c> and must not also mutate them via
/// <c>SaveChanges</c>.
/// </summary>
internal static class AnnouncementClaims
{
    /// <summary>
    /// How long a claim stays valid. A process that dies between claiming and settling leaves the
    /// row <see cref="AnnouncementStatus.Claimed"/> forever, so <see cref="ReleaseStaleClaimsAsync"/>
    /// reverts anything older than this. Comfortably longer than a cycle's worst-case send time
    /// (the per-cycle cap times the HTTP timeout plus the inter-message spacing), so it can only
    /// fire on an actually-abandoned claim.
    /// </summary>
    internal static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Atomically claims a row for delivery, but only if it is currently in one of
    /// <paramref name="from"/>. Returns true when this caller won the claim (and must therefore
    /// settle it), false when the row was already claimed or had moved on.
    /// </summary>
    internal static async Task<bool> TryClaimAsync(
        BlogAnnouncementsDbContext db,
        Guid platformPostId,
        DateTime nowUtc,
        IReadOnlyCollection<AnnouncementStatus> from,
        CancellationToken cancellationToken)
    {
        var affected = await db.AnnouncedBlogPosts
            .Where(p => p.PlatformPostId == platformPostId && from.Contains(p.Status))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.Status, AnnouncementStatus.Claimed)
                    .SetProperty(p => p.ClaimedUtc, nowUtc),
                cancellationToken);

        return affected == 1;
    }

    /// <summary>
    /// Writes the settled state of a claimed row and clears the claim. Pass the pre-claim status
    /// (and its <c>AnnouncedUtc</c>) to release a claim without changing anything — what a dry-run
    /// delivery needs.
    /// </summary>
    internal static Task SettleAsync(
        BlogAnnouncementsDbContext db,
        Guid platformPostId,
        AnnouncementStatus status,
        DateTime? announcedUtc,
        CancellationToken cancellationToken)
        => db.AnnouncedBlogPosts
            .Where(p => p.PlatformPostId == platformPostId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.Status, status)
                    .SetProperty(p => p.AnnouncedUtc, announcedUtc)
                    .SetProperty(p => p.ClaimedUtc, (DateTime?)null),
                cancellationToken);

    /// <summary>
    /// Reverts claims abandoned by a process that died mid-send (or was recycled) to
    /// <see cref="AnnouncementStatus.Failed"/>, so the next cycle retries them instead of leaving
    /// them stuck in flight. Failed is deliberate over Pending: the retry semantics are identical,
    /// and the dashboard then shows that something went wrong. Returns the number reverted.
    /// </summary>
    internal static Task<int> ReleaseStaleClaimsAsync(
        BlogAnnouncementsDbContext db,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = nowUtc - StaleClaimTimeout;

        return db.AnnouncedBlogPosts
            .Where(p => p.Status == AnnouncementStatus.Claimed
                && (p.ClaimedUtc == null || p.ClaimedUtc < cutoffUtc))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.Status, AnnouncementStatus.Failed)
                    .SetProperty(p => p.ClaimedUtc, (DateTime?)null),
                cancellationToken);
    }
}
