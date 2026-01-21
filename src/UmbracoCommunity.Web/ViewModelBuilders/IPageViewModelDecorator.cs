using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public interface IPageViewModelDecorator<in TContentModel>
    where TContentModel : IPublishedElement
    {
        /// <summary>
        /// Decorates the provided view model using properties from the provided <see cref="IPublishedContent"/>.
        /// </summary>
        Task DecorateAsync(PageViewModelBase viewModel, IPublishedContent publishedContent);
    }
}
