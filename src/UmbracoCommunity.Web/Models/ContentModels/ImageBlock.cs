using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class ImageBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
