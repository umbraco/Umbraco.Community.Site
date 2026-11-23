using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class ImageCarouselBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);

        public IEnumerable<ImageCarouselItem> Items =>
            Images?.Select(block => block.Content).OfType<ImageCarouselItem>().Where(item => item.Image != null)
            ?? Enumerable.Empty<ImageCarouselItem>();
    }
}
