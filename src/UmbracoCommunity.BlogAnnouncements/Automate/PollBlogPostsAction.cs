using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Umbraco.Automate.Core.Actions;
using UmbracoCommunity.BlogAnnouncements.Detection;

namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>Settings for <see cref="PollBlogPostsAction"/>. No inputs are required — the cache
/// file path is fixed (see the doc comment on <see cref="PollBlogPostsAction"/>).</summary>
public sealed class PollBlogPostsSettings
{
}

/// <summary>Output produced by <see cref="PollBlogPostsAction"/>.</summary>
public sealed class PollBlogPostsOutput
{
    public int Fetched { get; init; }
    public int New { get; init; }
    public int Skipped { get; init; }
}

/// <summary>
/// Umbraco Automate action that reads the community-blogs disk cache file — written by the host's
/// "Refresh Community Blogs Cache" action (<c>UmbracoCommunity.Web</c>) — and ingests its posts
/// into the tracking store (dedup, recency window). This is the fetch half of the pipeline; pair
/// with <see cref="AnnounceBlogPostsAction"/>, bound to this step's output, to complete a cycle.
/// The host's "Refresh Community Blogs Cache" automation must run at least once before this step
/// has anything to read.
///
/// Reads the file directly (via a fixed path derived from <see cref="IWebHostEnvironment.ContentRootPath"/>)
/// rather than referencing the host's <c>ICommunityBlogsService</c>/<c>CommunityBlogsData</c>
/// types, to preserve this project's independence from the host's feed models — see
/// <c>RegisterBlogAnnouncements.cs</c>. The JSON shape below must be kept in sync with
/// <c>CommunityBlogsData</c>/<c>CommunityBlogPost</c> in
/// <c>UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogPost.cs</c> and the file
/// path with <c>CommunityBlogsService</c> in the same folder.
/// </summary>
[Action(
    "umbracoCommunity.pollBlogPosts",
    "Poll Blog Posts",
    Description = "Reads the community-blogs cache file and ingests new posts into the tracking store (dedup, recency window).",
    Group = "Blog Announcements",
    Icon = "icon-rss")]
public sealed class PollBlogPostsAction : ActionBase<PollBlogPostsSettings, PollBlogPostsOutput>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IBlogAnnouncementDetector _detector;
    private readonly string _cacheFilePath;

    public PollBlogPostsAction(ActionInfrastructure infrastructure, IBlogAnnouncementDetector detector, IWebHostEnvironment hostEnvironment)
        : base(infrastructure)
    {
        _detector = detector;
        _cacheFilePath = Path.Combine(hostEnvironment.ContentRootPath, "umbraco", "Data", "TEMP", "CommunityBlogsCache", "community-blogs.json");
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(_cacheFilePath))
        {
            return ActionResult.Failed(
                new FileNotFoundException("Community blogs cache file not found — has the Refresh Community Blogs Cache automation run yet?", _cacheFilePath),
                StepRunErrorCategory.ConfigurationError);
        }

        CommunityBlogsCacheFile? cache;
        try
        {
            var json = await File.ReadAllTextAsync(_cacheFilePath, cancellationToken);
            cache = JsonSerializer.Deserialize<CommunityBlogsCacheFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ActionResult.Failed(ex, StepRunErrorCategory.InvalidResponse);
        }

        var candidates = (cache?.Posts ?? [])
            .Where(p => !string.IsNullOrEmpty(p.Id) && !string.IsNullOrEmpty(p.Title) && !string.IsNullOrEmpty(p.Url))
            .Select(p => new AnnouncementCandidatePost(
                p.Id!,
                p.Title!,
                p.Url!,
                p.Excerpt,
                p.CoverImageUrl,
                p.PublishedAt,
                p.AuthorName,
                p.AuthorAvatarUrl,
                p.AuthorProfileUrl))
            .ToList();

        var result = await _detector.PollAsync(candidates, cancellationToken);

        return Success(new PollBlogPostsOutput { Fetched = result.Fetched, New = result.New, Skipped = result.Skipped });
    }

    /// <summary>Mirrors the JSON shape <c>CommunityBlogsService</c> writes to disk (i.e.
    /// <c>CommunityBlogsData</c>/<c>CommunityBlogPost</c> serialized with
    /// <c>JsonSerializerDefaults.Web</c>). Kept local rather than shared with the host's types to
    /// preserve this project's independence — see this action's doc comment.</summary>
    private sealed record CommunityBlogsCacheFile(IReadOnlyList<CommunityBlogPostDto>? Posts);

    private sealed record CommunityBlogPostDto(
        string? Id,
        string? Title,
        string? Url,
        string? Excerpt,
        string? CoverImageUrl,
        DateTimeOffset PublishedAt,
        string? AuthorName,
        string? AuthorAvatarUrl,
        string? AuthorProfileUrl);
}
