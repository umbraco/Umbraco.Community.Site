using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ViewModels.Components;

namespace UmbracoCommunity.Web.Services;

public interface ISeoDataService
{
    Task<MetaTagsViewModel> BuildAsync(IPublishedContent currentPage);
}
