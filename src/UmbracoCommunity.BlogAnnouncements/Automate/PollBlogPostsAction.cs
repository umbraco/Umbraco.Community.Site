using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using UmbracoCommunity.BlogAnnouncements.Detection;

namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>Settings for <see cref="PollBlogPostsAction"/>.</summary>
public sealed class PollBlogPostsSettings
{
    [Field(
        Label = "Sphere Response Body",
        Description = "The raw JSON response body from the Sphere blog-posts HTTP GET step.",
        SupportsBindings = true)]
    public string ResponseBody { get; set; } = string.Empty;
}

/// <summary>Output produced by <see cref="PollBlogPostsAction"/>.</summary>
public sealed class PollBlogPostsOutput
{
    public int Fetched { get; init; }
    public int New { get; init; }
    public int Skipped { get; init; }
}

/// <summary>
/// Umbraco Automate action that parses a Sphere blog-posts API response and ingests it into the
/// tracking store (dedup, recency window) — the fetch half of the pipeline. Pair with
/// <see cref="AnnounceBlogPostsAction"/>, bound to this step's output, to complete a cycle. Kept
/// independent of the host's <c>CommunityBlogs</c> feed models, so this project needs no reference
/// to the host's feed types.
/// </summary>
[Action(
    "umbracoCommunity.pollBlogPosts",
    "Poll Blog Posts",
    Description = "Parses a Sphere blog-posts API response and ingests new posts into the tracking store (dedup, recency window).",
    Group = "Blog Announcements",
    Icon = "icon-rss")]
public sealed class PollBlogPostsAction : ActionBase<PollBlogPostsSettings, PollBlogPostsOutput>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IBlogAnnouncementDetector _detector;

    public PollBlogPostsAction(ActionInfrastructure infrastructure, IBlogAnnouncementDetector detector)
        : base(infrastructure)
    {
        _detector = detector;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PollBlogPostsSettings>();
        if (string.IsNullOrWhiteSpace(settings.ResponseBody))
        {
            return ActionResult.Failed(new ArgumentException("Sphere response body is required."), StepRunErrorCategory.Validation);
        }

        SphereBlogPostsResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<SphereBlogPostsResponse>(settings.ResponseBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ActionResult.Failed(ex, StepRunErrorCategory.InvalidResponse);
        }

        var candidates = (response?.Data ?? [])
            .Where(p => !string.IsNullOrEmpty(p.Id) && !string.IsNullOrEmpty(p.Title) && !string.IsNullOrEmpty(p.Url))
            .Select(p => new AnnouncementCandidatePost(
                p.Id!,
                p.Title!,
                p.Url!,
                p.Content,
                p.CoverImageUrl,
                p.PublishedAt,
                p.Author?.Name,
                p.Author?.AvatarUrl,
                p.Author?.ProfileUrl))
            .ToList();

        var result = await _detector.PollAsync(candidates, cancellationToken);

        return Success(new PollBlogPostsOutput { Fetched = result.Fetched, New = result.New, Skipped = result.Skipped });
    }

    /// <summary>Mirrors Sphere's <c>GET /v1/blog-posts</c> response shape. Kept local rather than
    /// shared with the host's <c>SphereBlogPostsDtos</c> to preserve this project's independence
    /// from the host's feed models.</summary>
    private sealed record SphereBlogPostsResponse(IReadOnlyList<SpherePostDto>? Data);

    private sealed record SpherePostDto(
        string? Id,
        string? Title,
        string? Url,
        string? Content,
        string? CoverImageUrl,
        DateTimeOffset PublishedAt,
        SphereAuthorDto? Author);

    private sealed record SphereAuthorDto(string? Name, string? ProfileUrl, string? AvatarUrl);
}
