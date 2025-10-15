using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Extensions;

public static class PublishedContentExtensions
{
    public static T As<T>(this IPublishedContent? content) where T : class, IPublishedContent =>
        content as T ?? throw new ArgumentException($"Provided published content is null or not composed of the expected content model: {typeof(T).FullName}. Content provided is {GetElementDescription(content)}.", nameof(content));

    public static Settings? GetSettingsNode(this IPublishedContent content)
    {
        var root = content.Root();
        if (root == null)
        {
            return null;
        }

        IPublishedContent? settingsNode = root.Children(x => x.ContentType.Alias == Settings.ModelTypeAlias)?.FirstOrDefault();
        return settingsNode?.As<Settings>();
    }

    public static NavigationSettings? GetNavigationSettings(this IPublishedContent content)
    {
        var settingsRoot = content.GetSettingsNode();
        if (settingsRoot == null)
        {
            return null;
        }
        var navSettings = settingsRoot.Children(x => x.ContentType.Alias == NavigationSettings.ModelTypeAlias)?.FirstOrDefault();
        return navSettings?.As<NavigationSettings>();
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

