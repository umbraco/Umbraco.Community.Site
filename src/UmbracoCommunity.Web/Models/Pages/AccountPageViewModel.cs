using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public class AccountPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public string? DisplayName { get; set; }
    public string? GitHubHandle { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}
