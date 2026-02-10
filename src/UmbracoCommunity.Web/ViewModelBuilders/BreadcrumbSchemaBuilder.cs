using Schema.NET;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.ViewModelBuilders;

/// <summary>
/// Builds BreadcrumbList schema from content hierarchy.
/// </summary>
internal class BreadcrumbSchemaBuilder
{
    private readonly UrlUtilities _urlUtilities;

    public BreadcrumbSchemaBuilder(UrlUtilities urlUtilities)
    {
        _urlUtilities = urlUtilities;
    }

    /// <summary>
    /// Builds a BreadcrumbList schema from the content's ancestors.
    /// </summary>
    public BreadcrumbList? Build(IPublishedContent content)
    {
        var ancestors = content.Ancestors().Reverse().ToList();

        // Need at least one ancestor (home page) plus current page for a meaningful breadcrumb
        if (ancestors.Count == 0)
        {
            return null;
        }

        var items = new List<IListItem>();
        var position = 1;

        // Add ancestors (home -> parent -> grandparent etc.)
        foreach (var ancestor in ancestors)
        {
            var item = CreateListItem(ancestor, position);
            if (item is not null)
            {
                items.Add(item);
                position++;
            }
        }

        // Add current page
        var currentItem = CreateListItem(content, position);
        if (currentItem is not null)
        {
            items.Add(currentItem);
        }

        if (items.Count == 0)
        {
            return null;
        }

        return new BreadcrumbList
        {
            ItemListElement = items
        };
    }

    private ListItem? CreateListItem(IPublishedContent content, int position)
    {
        var url = _urlUtilities.GetAbsoluteUrl(content);
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new ListItem
        {
            Position = position,
            Item = new WebPage
            {
                Id = uri,
                Name = content.Name
            }
        };
    }
}
