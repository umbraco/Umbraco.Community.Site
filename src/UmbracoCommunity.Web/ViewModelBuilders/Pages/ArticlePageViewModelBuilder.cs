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

            // Find the parent Blog page
            var blogPage = currentPage.AncestorOrSelf<Blog>();

            // Get the first author if one is set
            var authorContent = contentModel.Author as Author;
            ArticleAuthorViewModel? author = null;
            if (authorContent != null)
            {
                author = new ArticleAuthorViewModel
                {
                    Name = authorContent.Name ?? string.Empty,
                    AvatarUrl = authorContent.Avatar?.GetCropUrl(50, 50)
                };
            }

            return new ArticlePageViewModel(currentPage)
            {
                Teaser = contentModel.Teaser?.ToHtmlString(),
                BlockContent = ParseBlockGrid(contentModel.ContentBlocks),
                PublishDate = contentModel.PublishDate != default ? contentModel.PublishDate : currentPage.CreateDate,
                ReadTime = contentModel.ReadTime,
                Categories = contentModel.Categories?.ToList() ?? new List<IPublishedContent>(0),
                Tags = contentModel.Tags?.ToList() ?? new List<string>(0),
                Banner = contentModel.BannerContent,
                BlogUrl = blogPage?.Url(),
                Author = author
            };
        }
    }
}
