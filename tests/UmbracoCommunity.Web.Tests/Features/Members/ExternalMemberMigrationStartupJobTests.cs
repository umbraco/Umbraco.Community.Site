using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using UmbracoCommunity.Web.Features.Members.Migration;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Members;

public class ExternalMemberMigrationStartupJobTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();

    public ExternalMemberMigrationStartupJobTests()
    {
        // The resolved scope has no ExternalMemberMigrationService, so a run that gets past the gate
        // throws inside the job - which must be caught, not escape.
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(new ServiceCollection().BuildServiceProvider());
        _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
    }

    private ExternalMemberMigrationStartupJob CreateJob(
        bool runOnStartup = true,
        RuntimeLevel level = RuntimeLevel.Run,
        ServerRole role = ServerRole.Single)
    {
        var options = new Mock<IOptionsMonitor<ExternalMemberMigrationOptions>>();
        options.Setup(o => o.CurrentValue).Returns(new ExternalMemberMigrationOptions { RunOnStartup = runOnStartup });

        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.Setup(r => r.Level).Returns(level);

        var roleAccessor = new Mock<IServerRoleAccessor>();
        roleAccessor.Setup(r => r.CurrentServerRole).Returns(role);

        return new ExternalMemberMigrationStartupJob(
            options.Object,
            runtimeState.Object,
            roleAccessor.Object,
            _scopeFactory.Object,
            NullLogger<ExternalMemberMigrationStartupJob>.Instance);
    }

    [Fact]
    public void Runs_on_startup_by_default()
    {
        new ExternalMemberMigrationOptions().RunOnStartup.Should().BeTrue();
    }

    [Fact]
    public void Only_runs_on_the_scheduling_publisher_or_a_single_server()
    {
        CreateJob().ServerRoles.Should().BeEquivalentTo([ServerRole.Single, ServerRole.SchedulingPublisher]);
    }

    [Fact]
    public async Task Does_nothing_and_never_fires_again_when_switched_off()
    {
        var job = CreateJob(runOnStartup: false);

        job.Delay.Should().Be(Timeout.InfiniteTimeSpan);

        await job.RunJobAsync(CancellationToken.None);

        _scopeFactory.Verify(f => f.CreateScope(), Times.Never);
        job.HasRun.Should().BeTrue();
        job.Period.Should().Be(Timeout.InfiniteTimeSpan);
    }

    [Theory]
    [InlineData(RuntimeLevel.Boot)]
    [InlineData(RuntimeLevel.Install)]
    [InlineData(RuntimeLevel.Upgrade)]
    [InlineData(RuntimeLevel.BootFailed)]
    public async Task Waits_and_retries_while_the_runtime_level_is_not_Run(RuntimeLevel level)
    {
        var job = CreateJob(level: level);
        var retryPeriod = job.Period;

        await job.RunJobAsync(CancellationToken.None);

        _scopeFactory.Verify(f => f.CreateScope(), Times.Never);
        job.HasRun.Should().BeFalse();
        job.Period.Should().Be(retryPeriod).And.NotBe(Timeout.InfiniteTimeSpan);
    }

    [Theory]
    [InlineData(ServerRole.Unknown)]
    [InlineData(ServerRole.Subscriber)]
    public async Task Waits_and_retries_on_other_server_roles(ServerRole role)
    {
        var job = CreateJob(role: role);

        await job.RunJobAsync(CancellationToken.None);

        _scopeFactory.Verify(f => f.CreateScope(), Times.Never);
        job.HasRun.Should().BeFalse();
        job.Period.Should().NotBe(Timeout.InfiniteTimeSpan);
    }

    [Theory]
    [InlineData(ServerRole.Single)]
    [InlineData(ServerRole.SchedulingPublisher)]
    public async Task Runs_once_and_never_throws_when_the_migration_fails(ServerRole role)
    {
        var job = CreateJob(role: role);

        var first = () => job.RunJobAsync(CancellationToken.None);
        await first.Should().NotThrowAsync();
        await job.RunJobAsync(CancellationToken.None);

        _scopeFactory.Verify(f => f.CreateScope(), Times.Once);
        job.HasRun.Should().BeTrue();
        job.Period.Should().Be(Timeout.InfiniteTimeSpan);
    }
}

public class ExternalMemberMigrationLeaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a claim")]
    [InlineData("host:abc|not a date")]
    public void Treats_a_missing_or_unreadable_claim_as_free(string? value)
    {
        ExternalMemberMigrationLease.IsHeld(value, Now).Should().BeFalse();
    }

    [Fact]
    public void Honours_a_claim_that_has_not_expired()
    {
        ExternalMemberMigrationLease.IsHeld($"host:abc|{Now.AddMinutes(5):O}", Now).Should().BeTrue();
    }

    [Fact]
    public void Ignores_an_expired_claim()
    {
        ExternalMemberMigrationLease.IsHeld($"host:abc|{Now.AddSeconds(-1):O}", Now).Should().BeFalse();
    }
}
