using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;

namespace UmbracoCommunity.BlogAnnouncements.Monitoring;

/// <summary>Outcome of one heartbeat check.</summary>
public sealed record HeartbeatCheckResult(bool Stale, TimeSpan? Age, bool Alerted);

/// <summary>Checks whether the announcement pipeline is still completing cycles.</summary>
public interface IAnnouncementHeartbeatMonitor
{
    Task<HeartbeatCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the newest <c>AnnouncementRun</c> heartbeat and alerts when it is too old.
/// </summary>
/// <remarks>
/// This deliberately does not run inside Umbraco Automate. The outage it exists to catch was
/// Automate's own engine failing to start, which left every scheduled trigger paused — an
/// automation watching the pipeline would have been just as paused as the pipeline.
/// </remarks>
public sealed class AnnouncementHeartbeatMonitor : IAnnouncementHeartbeatMonitor
{
    private readonly IDbContextFactory<BlogAnnouncementsDbContext> _contextFactory;
    private readonly IHeartbeatAlerter _alerter;
    private readonly IOptionsMonitor<BlogAnnouncementsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AnnouncementHeartbeatMonitor> _logger;

    public AnnouncementHeartbeatMonitor(
        IDbContextFactory<BlogAnnouncementsDbContext> contextFactory,
        IHeartbeatAlerter alerter,
        IOptionsMonitor<BlogAnnouncementsOptions> options,
        TimeProvider time,
        ILogger<AnnouncementHeartbeatMonitor> logger)
    {
        _contextFactory = contextFactory;
        _alerter = alerter;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// The outage currently alerted about, identified by the last run that preceded it, plus when
    /// that alert was sent. Kept in memory only: if the process restarts mid-outage the next check
    /// alerts again, which is the behaviour we want — a restart is exactly when someone should be
    /// told the pipeline still isn't running.
    /// </summary>
    private DateTime? _alertedForRunUtc;
    private DateTime? _lastAlertUtc;

    public async Task<HeartbeatCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var monitoring = _options.CurrentValue.Monitoring;
        if (!monitoring.Enabled)
        {
            return new HeartbeatCheckResult(false, null, false);
        }

        var nowUtc = _time.GetUtcNow().UtcDateTime;

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var lastRunUtc = await db.AnnouncementRuns
            .OrderByDescending(r => r.RunUtc)
            .Select(r => (DateTime?)r.RunUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastRunUtc is null)
        {
            return new HeartbeatCheckResult(false, null, false);
        }

        var age = nowUtc - lastRunUtc.Value;
        if (age <= TimeSpan.FromMinutes(monitoring.StaleAfterMinutes))
        {
            // Healthy: forget the outage we were alerting about, so the next one alerts at once
            // instead of being mistaken for a continuation of it.
            _alertedForRunUtc = null;
            _lastAlertUtc = null;
            return new HeartbeatCheckResult(false, age, false);
        }

        if (!ShouldAlert(lastRunUtc.Value, nowUtc, monitoring))
        {
            _logger.LogDebug(
                "BlogAnnouncements pipeline still stale ({Age}), alert already sent at {LastAlertUtc}.",
                age, _lastAlertUtc);
            return new HeartbeatCheckResult(true, age, false);
        }

        _logger.LogWarning(
            "BlogAnnouncements pipeline has not completed a cycle for {Age}; last run was {LastRunUtc:u}.",
            age, lastRunUtc.Value);

        var delivered = await _alerter.AlertAsync(
            new StaleRunAlert(lastRunUtc.Value, age, nowUtc), cancellationToken);

        if (delivered)
        {
            // Only a delivered alert suppresses the next one — a webhook that refused it must not
            // buy itself six hours of silence.
            _alertedForRunUtc = lastRunUtc.Value;
            _lastAlertUtc = nowUtc;
        }

        return new HeartbeatCheckResult(true, age, delivered);
    }

    /// <summary>
    /// Alert when this is a different outage from the one already reported, or when the repeat
    /// interval has elapsed on the same one.
    /// </summary>
    private bool ShouldAlert(DateTime lastRunUtc, DateTime nowUtc, MonitoringOptions monitoring)
    {
        if (_alertedForRunUtc != lastRunUtc || _lastAlertUtc is null)
        {
            return true;
        }

        return nowUtc - _lastAlertUtc.Value >= TimeSpan.FromHours(monitoring.RepeatAfterHours);
    }
}
