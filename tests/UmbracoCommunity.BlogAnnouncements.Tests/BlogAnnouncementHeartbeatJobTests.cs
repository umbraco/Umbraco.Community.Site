using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using UmbracoCommunity.BlogAnnouncements.Monitoring;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class BlogAnnouncementHeartbeatJobTests
{
    private readonly Mock<IAnnouncementHeartbeatMonitor> _monitor = new();

    private BlogAnnouncementHeartbeatJob CreateJob(RuntimeLevel level = RuntimeLevel.Run)
    {
        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.Setup(r => r.Level).Returns(level);

        return new BlogAnnouncementHeartbeatJob(
            _monitor.Object,
            runtimeState.Object,
            NullLogger<BlogAnnouncementHeartbeatJob>.Instance);
    }

    [Fact]
    public void Only_runs_on_the_scheduling_publisher_or_a_single_server()
    {
        CreateJob().ServerRoles.Should().BeEquivalentTo([ServerRole.Single, ServerRole.SchedulingPublisher]);
    }

    [Fact]
    public void Waits_long_enough_after_start_up_for_a_pipeline_cycle_to_have_run()
    {
        // The pipeline runs every 15 minutes. Checking sooner than that after a restart would
        // alert about the gap the restart itself caused, every time a deploy follows an outage.
        CreateJob().Delay.Should().BeGreaterThan(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Checks_the_heartbeat_when_the_runtime_is_ready()
    {
        var job = CreateJob();

        await job.RunJobAsync(CancellationToken.None);

        _monitor.Verify(m => m.CheckAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Defers_while_the_runtime_is_not_running()
    {
        var job = CreateJob(RuntimeLevel.Install);

        await job.RunJobAsync(CancellationToken.None);

        _monitor.Verify(m => m.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_failing_check_never_escapes_the_job()
    {
        // An unhandled exception here would take down the recurring job runner, which would mean
        // the thing watching for silence goes silent itself.
        _monitor
            .Setup(m => m.CheckAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unreachable"));
        var job = CreateJob();

        var act = async () => await job.RunJobAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
