using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public interface IViewModelBuilder<out TViewModel>
    {
        /// <summary>
        /// Builds a view model model using properties from the provided <see cref="IPublishedContent"/>.
        /// </summary>
        TViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext);
    }

    public interface IViewModelBuilder<in TInputModel, out TViewModel>
    {
        /// <summary>
        /// Builds a view model model using properties from the provided <see cref="IPublishedContent"/> along with details from the provided input model.
        /// </summary>
        TViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext, TInputModel inputModel);
    }
}
