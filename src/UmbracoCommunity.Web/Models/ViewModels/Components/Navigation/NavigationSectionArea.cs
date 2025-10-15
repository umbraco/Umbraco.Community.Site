
namespace UmbracoCommunity.Web.Models.ViewModels.Components.Navigation
{
    public class NavigationSectionArea : INavigationElement
    {
        private readonly List<NavigationLink> _links = [];

        public NavigationSectionArea()
        {
        }

        public string? Headline { get; set; }

        public IReadOnlyList<NavigationLink> Links => _links.AsReadOnly();

        public void AddLink(NavigationLink navigationLink) => _links.Add(navigationLink);

        public object Clone() => new NavigationSectionArea();
    }
}