using Umbraco.Cms.Core.Models;

namespace UmbracoCommunity.Web.Models.ViewModels.Components.Navigation
{
    public class NavigationItem : NavigationLink, INavigationElement
    {
        public NavigationItem(Link link)
            : base(link) { }

        public NavigationItem(NavigationItem item)
            : base(item) { }

        public override object Clone() => new NavigationItem(this);
    }
}