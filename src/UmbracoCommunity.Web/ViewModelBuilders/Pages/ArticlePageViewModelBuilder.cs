using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class ArticlePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ArticlePageViewModel>
    {
        public ArticlePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            Article contentModel = currentPage.As<Article>();

            return new ArticlePageViewModel(currentPage)
            {
                Teaser = contentModel.Teaser?.ToHtmlString(),
                BlockContent = ParseBlockGrid(contentModel.ContentBlocks),
                PublishDate = contentModel.PublishDate != default ? contentModel.PublishDate : currentPage.CreateDate,
                ReadTime = contentModel.ReadTime
            };
        }
    }
}
