using Umbraco.Cms.Core.Models.Blocks;
using UmbracoCommunity.Common.Utilities;

namespace UmbracoCommunity.Web.Models.ViewModels.Blocks
{
    public class BlockGridRow
    {
        public IList<BlockGridItem> Blocks { get; set; } = [];
        public bool HasMultipleBlocks { get; set; } = false;
        public string Alias => GetAlias();
        public string IdHash { get; } = StringUtilities.RandomString(5);

        private string GetAlias()
        {
            if (!Blocks.Any()) return string.Empty;
            var aliases = Blocks.Select(x => x.Content.ContentType.Alias.Replace("Block", string.Empty)).Distinct().OrderBy(o => o);
            var combinedAlias = string.Join(string.Empty, aliases);
            return HasMultipleBlocks ? $"{combinedAlias}Row" : $"{combinedAlias}Block";
        }
    }
}
