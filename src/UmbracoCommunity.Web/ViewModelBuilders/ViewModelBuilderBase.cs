using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public abstract class ViewModelBuilderBase
    {
        public static string? GetWebPageName(ICompositionSeo contentModel)
        {
            if (contentModel is Home or EventsHome)
            {
                return contentModel.MetaTitle;
            }

            if (contentModel is Blog && !string.IsNullOrEmpty(contentModel.MetaTitle))
            {
                return contentModel.MetaTitle;
            }

            return (contentModel as IPublishedContent)?.Name;
        }
    }
}
