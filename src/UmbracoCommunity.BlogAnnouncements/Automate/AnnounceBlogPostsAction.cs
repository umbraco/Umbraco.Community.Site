using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using UmbracoCommunity.BlogAnnouncements.Detection;

namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>
/// Settings for <see cref="AnnounceBlogPostsAction"/>. The three counts are strings, not ints —
/// a numeric field renders as a native number input with browser-level validation that rejects a
/// <c>${ ... }</c> binding expression outright, since it isn't a valid number literal.
/// </summary>
public sealed class AnnounceBlogPostsSettings
{
    [Field(Label = "Fetched", Description = "The Fetched count from the Poll Blog Posts step.", SupportsBindings = true)]
    public string Fetched { get; set; } = "0";

    [Field(Label = "New", Description = "The New count from the Poll Blog Posts step.", SupportsBindings = true)]
    public string New { get; set; } = "0";

    [Field(Label = "Skipped", Description = "The Skipped count from the Poll Blog Posts step.", SupportsBindings = true)]
    public string Skipped { get; set; } = "0";
}

/// <summary>Output produced by <see cref="AnnounceBlogPostsAction"/>.</summary>
public sealed class AnnounceBlogPostsOutput
{
    public int Announced { get; init; }
    public int Failed { get; init; }
}

/// <summary>
/// Umbraco Automate action that delivers the queued (Pending/Failed) tracked posts to Discord and
/// records one <c>AnnouncementRun</c> heartbeat row combining its own numbers with the
/// <see cref="PollBlogPostsAction"/> step's — the deliver half of the pipeline. Bind this step's
/// <c>Fetched</c>/<c>New</c>/<c>Skipped</c> settings to the Poll step's output
/// (<c>${ steps.pollBlogPosts.fetched }</c> etc.).
/// </summary>
[Action(
    "umbracoCommunity.announceBlogPosts",
    "Announce Blog Posts",
    Description = "Delivers queued posts to Discord and records one AnnouncementRun row combining these numbers with the Poll Blog Posts step's.",
    Group = "Blog Announcements",
    Icon = "icon-rss")]
public sealed class AnnounceBlogPostsAction : ActionBase<AnnounceBlogPostsSettings, AnnounceBlogPostsOutput>
{
    private readonly IBlogAnnouncementDetector _detector;

    public AnnounceBlogPostsAction(ActionInfrastructure infrastructure, IBlogAnnouncementDetector detector)
        : base(infrastructure)
    {
        _detector = detector;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<AnnounceBlogPostsSettings>();

        if (!int.TryParse(settings.Fetched, out var fetched) ||
            !int.TryParse(settings.New, out var newCount) ||
            !int.TryParse(settings.Skipped, out var skipped))
        {
            return ActionResult.Failed(
                new ArgumentException("Fetched, New, and Skipped must all be numbers."),
                StepRunErrorCategory.Validation);
        }

        var result = await _detector.AnnounceQueuedAsync(fetched, newCount, skipped, cancellationToken);

        return Success(new AnnounceBlogPostsOutput { Announced = result.Announced, Failed = result.Failed });
    }
}
