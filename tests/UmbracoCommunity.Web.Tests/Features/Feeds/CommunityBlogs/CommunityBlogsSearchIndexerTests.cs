using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class CommunityBlogsSearchIndexerTests
{
    [Fact]
    public void BuildValueSets_MapsPostFieldsAndKeysById()
    {
        var post = new CommunityBlogPost(
            Id: "post-1",
            Title: "Hello Umbraco",
            Url: "https://example.com/hello",
            Excerpt: "An excerpt",
            CoverImageUrl: null,
            PublishedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            AuthorName: "Jane Doe",
            AuthorAvatarUrl: null,
            AuthorProfileUrl: null);

        var data = new CommunityBlogsData(new[] { post }, DateTimeOffset.UtcNow);

        var sets = CommunityBlogsSearchIndexer.BuildValueSets(data).ToList();

        Assert.Single(sets);
        var vs = sets[0];
        Assert.Equal("post-1", vs.Id);
        Assert.Equal(CommunityBlogsSearchIndexer.Category, vs.Category);
        Assert.Equal("Hello Umbraco", vs.Values[CommunityBlogsSearchIndexer.FieldTitle].Single());
        Assert.Equal("An excerpt", vs.Values[CommunityBlogsSearchIndexer.FieldExcerpt].Single());
        Assert.Equal("Jane Doe", vs.Values[CommunityBlogsSearchIndexer.FieldAuthor].Single());
        Assert.Equal("https://example.com/hello", vs.Values[CommunityBlogsSearchIndexer.FieldUrl].Single());
    }

    [Fact]
    public void BuildValueSets_NullOptionalFields_MapToEmptyStrings()
    {
        var post = new CommunityBlogPost(
            Id: "post-2",
            Title: "No excerpt",
            Url: "https://example.com/no-excerpt",
            Excerpt: null,
            CoverImageUrl: null,
            PublishedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            AuthorName: null,
            AuthorAvatarUrl: null,
            AuthorProfileUrl: null);

        var data = new CommunityBlogsData(new[] { post }, DateTimeOffset.UtcNow);

        var vs = CommunityBlogsSearchIndexer.BuildValueSets(data).Single();

        Assert.Equal(string.Empty, vs.Values[CommunityBlogsSearchIndexer.FieldExcerpt].Single());
        Assert.Equal(string.Empty, vs.Values[CommunityBlogsSearchIndexer.FieldAuthor].Single());
    }

    [Fact]
    public void BuildValueSets_MultiplePosts_OneSetEachWithIsoPublishedAt()
    {
        var posts = new[]
        {
            new CommunityBlogPost("a", "A", "https://example.com/a", "x", null,
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), "Au", null, null),
            new CommunityBlogPost("b", "B", "https://example.com/b", "y", null,
                new DateTimeOffset(2026, 6, 2, 9, 30, 0, TimeSpan.Zero), "Bu", null, null),
        };

        var data = new CommunityBlogsData(posts, DateTimeOffset.UtcNow);

        var sets = CommunityBlogsSearchIndexer.BuildValueSets(data).ToList();

        Assert.Equal(2, sets.Count);
        var first = sets.Single(s => s.Id == "a");
        Assert.Equal("2026-06-01T00:00:00.0000000Z", first.Values[CommunityBlogsSearchIndexer.FieldPublishedAt].Single());
    }
}
