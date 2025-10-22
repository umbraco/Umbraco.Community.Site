using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;

namespace UmbracoCommunity.Web.Models.ViewModels.Components
{
    public class FooterViewModel
    {
        public MediaWithCrops? Logo { get; set; }

        public BlockGridModel? LinkColumns { get; set; }
    }
}