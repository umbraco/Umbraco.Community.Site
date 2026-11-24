using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace UmbracoCommunity.BlogAnnouncements.Monitoring;

/// <summary>
/// Runs the heartbeat check on Umbraco's own recurring-job runner — deliberately not on Umbraco
/// Automate's scheduler, which is the component whose failure this is here to notice.
/// </summary>
public sealed class BlogAnnouncementHeartbeatJob : RecurringBackgroundJobBase
{
    /// <summary>
    /// Four times an hour, matching the pipeline's own cadence. The alert's repeat interval, not
    /// this period, is what stops a long outage becoming a stream of messages.
    /// </summary>
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromMinutes(15);

    private static readonly ServerRole[] AllowedServerRoles = [ServerRole.Single, ServerRole.SchedulingPublisher];

    private readonly IAnnouncementHeartbeatMonitor _monitor;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<BlogAnnouncementHeartbeatJob> _logger;

    public BlogAnnouncementHeartbeatJob(
        IAnnouncementHeartbeatMonitor monitor,
        IRuntimeState runtimeState,
        ILogger<BlogAnnouncementHeartbeatJob> logger)
        : base(CheckPeriod)
    {
        _monitor = monitor;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    /// <summary>
    /// Longer than the pipeline's own 15-minute cadence, so a restart gets at least one chance to
    /// complete a cycle before we judge it. Checking sooner would alert about the gap the restart
    /// itself caused, every time a deploy follows an outage.
    /// </summary>
    public override TimeSpan Delay => TimeSpan.FromMinutes(20);

    public override ServerRole[] ServerRoles => AllowedServerRoles;

    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            _logger.LogDebug(
                "Deferring the blog-announcement heartbeat check: runtime level is {Level}.",
                _runtimeState.Level);
            return;
        }

        try
        {
            await _monitor.CheckAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let this escape: an unhandled exception would stop the recurring job, and the
            // watchdog going quiet is the one failure nobody would be told about.
            _logger.LogWarning(ex, "The blog-announcement heartbeat check failed.");
        }
    }
}
