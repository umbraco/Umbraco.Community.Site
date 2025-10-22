using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public interface IPageViewModelDecorator<in TContentModel>
    where TContentModel : IPublishedElement
    {
        /// <summary>
        /// Decorates the provided view model model using properties from the provided <see cref="IPublishedContent"/>.
        /// </summary>
        void Decorate(PageViewModelBase viewModel, IPublishedContent? contentModel);
    }

    public interface IViewModelDecorator<in TContentModel, in TInputModel>
        where TContentModel : IPublishedElement
    {
        /// <summary>
        /// Decorates the provided view model model using properties from the provided <see cref="IPublishedContent"/> and any further data provided in the input model.
        /// </summary>
        void Decorate(PageViewModelBase viewModel, IPublishedContent? contentModel, TInputModel inputModel);
    }
}
