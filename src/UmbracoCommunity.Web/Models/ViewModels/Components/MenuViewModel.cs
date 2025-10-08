using Umbraco.Cms.Core.Models;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class MenuViewModel : NavigationViewModelBase, ICloneable
{
    private readonly List<INavigationElement> _topLevelNavigationItems = [];
    private readonly List<NavigationLink> _ctaButtons = [];

    public MenuViewModel() { }

    private MenuViewModel(MenuViewModel viewModel)
    {
        _topLevelNavigationItems = viewModel._topLevelNavigationItems.Select(x => (INavigationElement)x.Clone()).ToList();
        _ctaButtons = viewModel.CallToActionButtons.Select(x => (NavigationLink)x.Clone()).ToList();
        SearchPageUrl = viewModel.SearchPageUrl;
        HeroLayout = viewModel.HeroLayout;
    }

    public bool HasSearchPage => !string.IsNullOrEmpty(SearchPageUrl);

    public bool HasHeroLayout => !string.IsNullOrEmpty(HeroLayout);

    public IReadOnlyList<INavigationElement> TopLevelNavigationItems => _topLevelNavigationItems.AsReadOnly();

    public IReadOnlyList<NavigationLink> CallToActionButtons => _ctaButtons;

    public string? SearchPageUrl { get; set; }

    public string? HeroLayout { get; set; }

    public void AddCtaButton(Link link) => _ctaButtons.Add(new NavigationLink(link));

    public void ClearCtaButtons() => _ctaButtons.Clear();

    public void AddTopLevelNavigationItem(INavigationElement navigationElement) => _topLevelNavigationItems.Add(navigationElement);

    public object Clone() => new MenuViewModel(this);

    public interface INavigationElement : ICloneable { }

    public class NavigationItem : NavigationLink, INavigationElement
    {
        public NavigationItem(Link link)
            : base(link) { }

        public NavigationItem(NavigationItem item)
            : base(item) { }

        public override object Clone() => new NavigationItem(this);
    }

    public class NavigationDropdown : INavigationElement
    {
        private readonly List<NavigationSection> _sections = [];

        public NavigationDropdown(string name, decimal columnsCount = 1)
        {
            Name = name;
            ColumnsCount = columnsCount;
        }

        public NavigationDropdown(NavigationDropdown dropdownItem)
        {
            Name = dropdownItem.Name;
            ColumnsCount = dropdownItem.ColumnsCount;

            foreach (NavigationSection section in dropdownItem.Sections)
            {
                _sections.Add((NavigationSection)section.Clone());
            }
        }

        public string Name { get; }

        public decimal ColumnsCount { get; }

        public IReadOnlyList<NavigationSection> Sections => _sections.AsReadOnly();

        public void AddSection(NavigationSection section) => _sections.Add(section);

        public object Clone() => new NavigationDropdown(this);
    }

    public class NavigationSection : ICloneable
    {
        private readonly List<NavigationSectionItem> _sectionItems = [];

        public NavigationSection() { }

        public NavigationSection(NavigationSection section)
        {
            Headline = section.Headline;

            foreach (NavigationSectionItem sectionItem in section.Items)
            {
                _sectionItems.Add((NavigationSectionItem)sectionItem.Clone());
            }
        }

        public string? Headline { get; set; }

        public IReadOnlyList<NavigationSectionItem> Items => _sectionItems.AsReadOnly();

        public void AddSectionItem(NavigationSectionItem sectionItem) => _sectionItems.Add(sectionItem);

        public object Clone() => new NavigationSection(this);
    }

    public class NavigationSectionItem : NavigationLink
    {
        public NavigationSectionItem(
            Link link,
            string? iconUrl = null,
            string? caption = null)
            : base(link)
        {
            IconUrl = iconUrl ?? string.Empty;
            Caption = caption ?? string.Empty;
        }

        public NavigationSectionItem(NavigationSectionItem item)
            : base(item)
        {
            IconUrl = item.IconUrl;
            Caption = item.Caption;
        }

        public string IconUrl { get; }
        public string Caption { get; }

        public override object Clone() => new NavigationSectionItem(this);
    }
}
