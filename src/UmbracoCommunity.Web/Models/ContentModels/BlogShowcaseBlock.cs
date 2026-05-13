using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class BlogShowcaseBlock
    {
        private const int DefaultPostCount = 3;
        private const int MaxPostCount = 12;

        public string IdHash { get; } = StringUtilities.RandomString(5);

        public int ResolvedNumberOfPostsToShow =>
            NumberOfPostsToShow switch
            {
                <= 0 => DefaultPostCount,
                > MaxPostCount => MaxPostCount,
                _ => NumberOfPostsToShow
            };
    }
}
