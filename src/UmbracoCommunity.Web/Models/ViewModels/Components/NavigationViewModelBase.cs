using Umbraco.Cms.Core.Models;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class NavigationViewModelBase
{
    public class NavigationLink : ICloneable
    {
        public NavigationLink(Link link)
        {
            Text = link.Name ?? string.Empty;
            Url = link.Url ?? string.Empty;
            Target = link.Target ?? "_self";
        }

        public NavigationLink(NavigationLink link)
        {
            Text = link.Text;
            Url = link.Url;
            Target = link.Target;
        }

        public string Text { get; set; }

        public string Url { get; set; }

        public string Target { get; set; }

        public virtual object Clone() => new NavigationLink(this);
    }
}
