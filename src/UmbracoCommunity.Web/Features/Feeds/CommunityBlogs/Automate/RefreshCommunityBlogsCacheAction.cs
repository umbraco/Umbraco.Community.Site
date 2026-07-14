using Umbraco.Automate.Core.Actions;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs.Automate;

/// <summary>Settings for <see cref="RefreshCommunityBlogsCacheAction"/>. No inputs are required.</summary>
public sealed class RefreshCommunityBlogsCacheSettings
{
}

/// <summary>Output produced by <see cref="RefreshCommunityBlogsCacheAction"/>.</summary>
public sealed class RefreshCommunityBlogsCacheOutput
{
    public int PostCount { get; init; }
    public DateTimeOffset LastUpdatedUtc { get; init; }
}

/// <summary>
/// Umbraco Automate action that re-aggregates community blog posts from Sphere and persists them
/// (memory + disk cache + search index) via <see cref="ICommunityBlogsService"/> — the single
/// fetch step other automations (e.g. the Discord announcement pipeline in
/// <c>UmbracoCommunity.BlogAnnouncements</c>) rely on by reading the resulting disk cache file
/// instead of calling Sphere themselves.
/// </summary>
[Action(
    "umbracoCommunity.refreshCommunityBlogsCache",
    "Refresh Community Blogs Cache",
    Description = "Re-aggregates community blog posts from Sphere and refreshes the memory/disk cache and search index used by the blog showcase and RSS feed.",
    Group = "Community Blogs",
    Icon = "icon-rss")]
public sealed class RefreshCommunityBlogsCacheAction : ActionBase<RefreshCommunityBlogsCacheSettings, RefreshCommunityBlogsCacheOutput>
{
    private readonly ICommunityBlogsService _service;

    public RefreshCommunityBlogsCacheAction(ActionInfrastructure infrastructure, ICommunityBlogsService service)
        : base(infrastructure)
    {
        _service = service;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        await _service.RefreshAsync(cancellationToken);

        var data = _service.GetData();

        return Success(new RefreshCommunityBlogsCacheOutput
        {
            PostCount = data.Posts.Count,
            LastUpdatedUtc = data.LastUpdatedUtc,
        });
    }
}
