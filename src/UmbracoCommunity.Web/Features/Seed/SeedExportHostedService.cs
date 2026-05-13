using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace UmbracoCommunity.Web.Features.Seed;

/// <summary>
/// Re-generates the seed snapshot zip once a day at 20:15 UTC.
/// Only runs on the scheduling-publisher / single server role.
/// </summary>
public sealed class SeedExportHostedService : RecurringHostedServiceBase
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeOnly RunAtUtc = new(20, 15);

    private readonly ISeedExportService _exportService;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly ILogger<SeedExportHostedService> _logger;

    public SeedExportHostedService(
        ISeedExportService exportService,
        IServerRoleAccessor serverRoleAccessor,
        ILogger<SeedExportHostedService> logger)
        : base(logger, Interval, ComputeInitialDelay(RunAtUtc, DateTimeOffset.UtcNow))
    {
        _exportService = exportService;
        _serverRoleAccessor = serverRoleAccessor;
        _logger = logger;
    }

    public override async Task PerformExecuteAsync(object? state)
    {
        var role = _serverRoleAccessor.CurrentServerRole;
        if (role is not (ServerRole.SchedulingPublisher or ServerRole.Single))
        {
            _logger.LogDebug("Skipping seed export — current server role {Role} is not scheduling-publisher.", role);
            return;
        }

        try
        {
            await _exportService.RegenerateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled seed export threw unexpectedly.");
        }
    }

    internal static TimeSpan ComputeInitialDelay(TimeOnly runAtUtc, DateTimeOffset nowUtc)
    {
        var todayRun = new DateTimeOffset(
            nowUtc.Year, nowUtc.Month, nowUtc.Day,
            runAtUtc.Hour, runAtUtc.Minute, 0, TimeSpan.Zero);

        var next = todayRun > nowUtc ? todayRun : todayRun.AddDays(1);
        return next - nowUtc;
    }
}
