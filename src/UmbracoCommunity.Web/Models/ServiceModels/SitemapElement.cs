namespace UmbracoCommunity.Web.Models.ServiceModels
{
    public class SitemapElement
    {
        public required Uri Url { get; init; }

        public required DateTime LastModified { get; init; }
    }
}
