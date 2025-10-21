using Umbraco.Cms.Core.Models;
using UmbracoCommunity.Web.Models.ViewModels.Components.Navigation;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class MenuViewModel
{
    private readonly List<INavigationElement> _topLevelNavigationItems = [];

    public MenuViewModel() { }

    public bool HasSearchPage => !string.IsNullOrEmpty(SearchPageUrl);

    public bool HasHeroLayout => !string.IsNullOrEmpty(HeroLayout);

    public string? SearchPageUrl { get; set; }

    public string? HeroLayout { get; set; }

    public MediaWithCrops? Logo { get; set; }

    public NavigationLink? CallToActionButton { get; set; }

    public IReadOnlyList<INavigationElement> TopLevelNavigationItems => _topLevelNavigationItems.AsReadOnly();

    public void AddTopLevelNavigationItem(INavigationElement navigationElement) => _topLevelNavigationItems.Add(navigationElement);
}
