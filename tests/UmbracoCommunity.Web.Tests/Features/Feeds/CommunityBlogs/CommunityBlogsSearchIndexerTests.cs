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
        Assert.Equal("Hello Umbraco", vs.Values["title"].Single());
        Assert.Equal("An excerpt", vs.Values["excerpt"].Single());
        Assert.Equal("Jane Doe", vs.Values["author"].Single());
        Assert.Equal("https://example.com/hello", vs.Values["url"].Single());
    }
}
