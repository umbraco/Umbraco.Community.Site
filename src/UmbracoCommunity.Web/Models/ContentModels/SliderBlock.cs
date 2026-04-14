using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class SliderBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
