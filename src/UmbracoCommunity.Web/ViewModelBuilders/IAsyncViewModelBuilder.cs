using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public interface IAsyncViewModelBuilder<TViewModel>
    {
        /// <summary>
        /// Builds a view model model using properties from the provided <see cref="IPublishedContent"/>.
        /// </summary>
        Task<TViewModel> Build(IPublishedContent currentPage, IUmbracoContext umbracoContext);
    }

    public interface IAsyncViewModelBuilder<in TInputModel, TViewModel>
    {
        /// <summary>
        /// Builds a view model model using properties from the provided <see cref="IPublishedContent"/> along with details from the provided input model.
        /// </summary>
        Task<TViewModel> BuildAsync(IPublishedContent currentPage, IUmbracoContext umbracoContext, TInputModel inputModel);
    }
}
