using UmbracoCommunity.Common.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class ColumnComparisonItem
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
