using System.Globalization;
using Examine;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class CommunityBlogsSearchIndexer : ICommunityBlogsIndexer
{
    public const string IndexName = "CommunityBlogsIndex";
    public const string Category = "communityBlogPost";

    public const string FieldTitle = "title";
    public const string FieldExcerpt = "excerpt";
    public const string FieldAuthor = "author";
    public const string FieldUrl = "url";
    public const string FieldPublishedAt = "publishedAt";

    private readonly IExamineManager _examineManager;
    private readonly ILogger<CommunityBlogsSearchIndexer> _logger;

    public CommunityBlogsSearchIndexer(IExamineManager examineManager, ILogger<CommunityBlogsSearchIndexer> logger)
    {
        _examineManager = examineManager;
        _logger = logger;
    }

    public static IEnumerable<ValueSet> BuildValueSets(CommunityBlogsData data)
    {
        foreach (var post in data.Posts)
        {
            var values = new Dictionary<string, object>
            {
                [FieldTitle] = post.Title ?? string.Empty,
                [FieldExcerpt] = post.Excerpt ?? string.Empty,
                [FieldAuthor] = post.AuthorName ?? string.Empty,
                [FieldUrl] = post.Url ?? string.Empty,
                [FieldPublishedAt] = post.PublishedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            };
            yield return new ValueSet(post.Id, Category, values);
        }
    }

    public void Rebuild(CommunityBlogsData data)
    {
        if (!_examineManager.TryGetIndex(IndexName, out var index))
        {
            _logger.LogWarning("Community blogs index '{Index}' not found; skipping indexing.", IndexName);
            return;
        }

        try
        {
            // Small, fully-replaced set: wipe and re-add so removed posts don't linger.
            index.CreateIndex();
            index.IndexItems(BuildValueSets(data));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rebuild community blogs index.");
        }
    }
}
