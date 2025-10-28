using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Models.ViewModels.Components
{
    public class FooterViewModel
    {
        public MediaWithCrops? Logo { get; set; }

        public BlockGridModel? LinkColumns { get; set; }

        public List<BlockGridItem<LinkWithIcon>> SocialLinks { get; set; } = [];

        public List<Link>? BottomLinks { get; set; }
    }
}