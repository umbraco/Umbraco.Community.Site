namespace UmbracoCommunity.Web.Models.ViewModels.Components.Navigation
{
    public class NavigationMenuItem : INavigationElement
    {
        private readonly List<NavigationSectionArea> _sections = [];

        public NavigationMenuItem(string title)
        {
            LinkTitle = title;
        }

        public string LinkTitle { get; set; }

        public IReadOnlyList<NavigationSectionArea> Links => _sections.AsReadOnly();

        public void AddSection(NavigationSectionArea section) => _sections.Add(section);

        public object Clone() => new NavigationMenuItem(LinkTitle);
    }
}