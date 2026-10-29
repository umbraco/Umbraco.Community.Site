using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>
/// Runs <see cref="ExternalMemberMigrationService.MigrateAsync"/> once, shortly after the application
/// starts, so content-based members are converted without anyone calling the backoffice API.
/// </summary>
/// <remarks>
/// <para>
/// Why a recurring background job rather than an <c>UmbracoApplicationStartedNotification</c> handler:
/// with the default elected server role, an instance's role is <see cref="ServerRole.Unknown"/> until
/// Umbraco's <c>TouchServerJob</c> first registers it (15s after start-up), so a started-notification
/// handler would see <c>Unknown</c> and could never tell a scheduling publisher from a subscriber. Umbraco's
/// background job runner waits for <see cref="RuntimeLevel.Run"/>, the right server role and MainDom,
/// re-checking every <see cref="RecurringBackgroundJobBase.IgnoredDelay"/> until they hold. The same
/// conditions are checked again in <see cref="RunJobAsync"/> so the rules live (and are tested) here too.
/// </para>
/// <para>
/// It runs once per process: after the first real attempt the period becomes infinite. Load-balanced
/// instances can't both convert members because only the scheduling publisher runs it, and
/// <see cref="ExternalMemberMigrationLease"/> guards against an overlap during a role hand-over.
/// Nothing escapes: failures are logged and the next start-up tries again.
/// </para>
/// </remarks>
public sealed class ExternalMemberMigrationStartupJob : RecurringBackgroundJobBase
{
    /// <summary>Retry cadence while the run conditions don't hold yet (e.g. the server role is still unknown).</summary>
    private static readonly TimeSpan RetryPeriod = TimeSpan.FromMinutes(1);

    private static readonly ServerRole[] AllowedServerRoles = [ServerRole.Single, ServerRole.SchedulingPublisher];

    private readonly IOptionsMonitor<ExternalMemberMigrationOptions> _options;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExternalMemberMigrationStartupJob> _logger;
    private int _hasRun;

    public ExternalMemberMigrationStartupJob(
        IOptionsMonitor<ExternalMemberMigrationOptions> options,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IServiceScopeFactory scopeFactory,
        ILogger<ExternalMemberMigrationStartupJob> logger)
        : base(RetryPeriod)
    {
        _options = options;
        _runtimeState = runtimeState;
        _serverRoleAccessor = serverRoleAccessor;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Shortly after start-up, once the server role has been elected. Infinite when the run is switched
    /// off, so the job never fires.
    /// </summary>
    public override TimeSpan Delay =>
        _options.CurrentValue.RunOnStartup ? TimeSpan.FromSeconds(30) : Timeout.InfiniteTimeSpan;

    public override ServerRole[] ServerRoles => AllowedServerRoles;

    /// <summary>True once the job has made its one attempt (or found it is switched off).</summary>
    internal bool HasRun => Volatile.Read(ref _hasRun) == 1;

    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        if (HasRun)
        {
            return;
        }

        if (!_options.CurrentValue.RunOnStartup)
        {
            _logger.LogDebug("External member migration on start-up is switched off ({Section}:RunOnStartup).", ExternalMemberMigrationOptions.SectionName);
            MarkDone();
            return;
        }

        // Not ready yet: leave the period alone so the job checks again.
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            _logger.LogDebug("Deferring the external member migration: runtime level is {Level}.", _runtimeState.Level);
            return;
        }

        var role = _serverRoleAccessor.CurrentServerRole;
        if (!AllowedServerRoles.Contains(role))
        {
            _logger.LogDebug("Deferring the external member migration: server role is {Role}.", role);
            return;
        }

        MarkDone();
        await RunMigrationAsync();
    }

    private void MarkDone()
    {
        Interlocked.Exchange(ref _hasRun, 1);
        Period = Timeout.InfiniteTimeSpan;
    }

    private async Task RunMigrationAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ExternalMemberMigrationService>();

            // Not linked to shutdown: stopping half way through a member would be worse than finishing the batch.
            var report = await service.MigrateAsync(limit: null, CancellationToken.None);

            if (report.Items.Count == 0)
            {
                _logger.LogInformation(
                    "External member migration on start-up: no content-based members left to convert. It can be switched off with {Section}:RunOnStartup = false.",
                    ExternalMemberMigrationOptions.SectionName);
                return;
            }

            foreach (var item in report.Items.Where(i => i.Outcome is ExternalMemberMigrationOutcome.Skipped or ExternalMemberMigrationOutcome.Failed))
            {
                _logger.LogWarning(
                    "External member migration: {Outcome} member {MemberKey} ({UserName}): {Reason}",
                    item.Outcome,
                    item.Key,
                    item.UserName,
                    item.Reason);
            }

            _logger.LogInformation(
                "External member migration on start-up finished: {Migrated} migrated, {Skipped} skipped, {Failed} failed.",
                report.Migrated,
                report.Skipped,
                report.Failed);
        }
        catch (ExternalMemberMigrationAlreadyRunningException ex)
        {
            _logger.LogInformation("Skipping the external member migration on start-up: {Reason}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External member migration on start-up failed; it is retried on the next start-up.");
        }
    }
}
