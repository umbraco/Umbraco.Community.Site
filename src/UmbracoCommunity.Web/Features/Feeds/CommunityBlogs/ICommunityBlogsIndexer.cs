namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public interface ICommunityBlogsIndexer
{
    /// <summary>Rebuilds the community blogs Examine index from the given data (clears, then re-adds).</summary>
    void Rebuild(CommunityBlogsData data);
}
