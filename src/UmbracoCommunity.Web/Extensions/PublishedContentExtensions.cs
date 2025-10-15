using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Extensions;

public static class PublishedContentExtensions
{
    public static T As<T>(this IPublishedContent? content) where T : class, IPublishedContent =>
        content as T ?? throw new ArgumentException($"Provided published content is null or not composed of the expected content model: {typeof(T).FullName}. Content provided is {GetElementDescription(content)}.", nameof(content));

    public static NavigationSettings? GetNavigationSettings(this IPublishedContent content)
    {
        IPublishedContent? settingsNode = content.Children.FirstOrDefault(x => x.ContentType.Alias == NavigationSettings.ModelTypeAlias);
        return settingsNode?.As<NavigationSettings>();
    }

    private static string GetElementDescription(IPublishedContent? element)
    {
        if (element is null)
        {
            return "null";
        }

        return $" of content type alias: {element.ContentType.Alias}";
    }
}

