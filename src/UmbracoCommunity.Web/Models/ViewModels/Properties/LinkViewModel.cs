using Umbraco.Cms.Core.Models;

namespace UmbracoCommunity.Web.Models.ViewModels.Properties;

public class LinkViewModel
{
    public LinkViewModel(Link? link)
    {
        Url = link?.Url ?? "/";
        Target = link?.Target ?? "_self";
        Name = link?.Name ?? string.Empty;
    }

    public LinkViewModel(string url, string? target, string? name)
    {
        Url = url;
        Target = target ?? "_self";
        Name = name ?? string.Empty;
    }

    public string Url { get; }

    public string Target { get; }

    public string Name { get; set; }
}
