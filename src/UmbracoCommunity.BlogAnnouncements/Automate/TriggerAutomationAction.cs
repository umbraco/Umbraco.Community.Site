using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Triggers;

namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>
/// Umbraco Automate action that starts a run on another automation, in-process — no HTTP loopback.
/// Lets a Manual-trigger "test run" automation kick off a Scheduled-trigger automation on demand,
/// since Automate only supports one trigger per automation (<c>Automation.Trigger</c> is a single
/// nullable field, not a collection). Mirrors the exact checks
/// <c>TriggerAutomationController</c> performs for its "Run now" endpoint.
/// </summary>
[Action(
    "umbracoCommunity.triggerAutomation",
    "Trigger Automation",
    Description = "Starts a run on another automation by ID — lets a Manual trigger kick off a Scheduled automation on demand.",
    Group = "Automation",
    Icon = "icon-play")]
public sealed class TriggerAutomationAction : ActionBase<TriggerAutomationSettings, TriggerAutomationOutput>
{
    private readonly IAutomationService _automationService;
    private readonly IAutomationExecutor _executor;
    private readonly ICircuitBreakerService _circuitBreaker;

    public TriggerAutomationAction(
        ActionInfrastructure infrastructure,
        IAutomationService automationService,
        IAutomationExecutor executor,
        ICircuitBreakerService circuitBreaker)
        : base(infrastructure)
    {
        _automationService = automationService;
        _executor = executor;
        _circuitBreaker = circuitBreaker;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<TriggerAutomationSettings>();
        if (!Guid.TryParse(settings.AutomationId, out var automationId))
        {
            return ActionResult.Failed(
                new ArgumentException("Automation ID must be a valid GUID."),
                StepRunErrorCategory.Validation);
        }

        var automation = await _automationService.GetAutomationAsync(automationId, cancellationToken);
        if (automation is null)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Automation '{automationId}' was not found."),
                StepRunErrorCategory.Validation);
        }

        if (automation.Status != AutomationStatus.Published)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Automation '{automation.Name}' must be published to be triggered."),
                StepRunErrorCategory.ConfigurationError);
        }

        if (!await _circuitBreaker.IsRunAllowedAsync(automationId, TriggerInitiatorType.System, cancellationToken))
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Automation '{automation.Name}' is auto-disabled by the circuit breaker."),
                StepRunErrorCategory.ConfigurationError);
        }

        var runId = await _executor.ExecuteAsync(
            automation,
            TriggerInitiatorType.System,
            initiatorId: null,
            triggerOutputData: null,
            cancellationToken);

        return Success(new TriggerAutomationOutput { RunId = runId });
    }
}
