using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class UpcomingEventsBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
