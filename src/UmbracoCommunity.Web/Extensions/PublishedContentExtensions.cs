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

    public static NavigationSettings? GetNavigationSettings(this IPublishedContent content, IPublishedContent? settingsNode = null)
    {
        Settings? settingsRoot = null;
        if (settingsNode == null || settingsNode is not Settings)
        {
            settingsRoot = content.GetSettingsNode();
            if (settingsRoot == null)
            {
                return null;
            }
        }
        else
        {
            settingsRoot = settingsNode as Settings;
        }
        var navSettings = settingsRoot?.Children(x => x.ContentType.Alias == NavigationSettings.ModelTypeAlias)?.FirstOrDefault();
        return navSettings?.As<NavigationSettings>();
    }

    public static SocialSettings? GetSocialSettings(this IPublishedContent content, IPublishedContent? settingsNode = null)
    {
        Settings? settingsRoot = null;
        if (settingsNode == null || settingsNode is not Settings)
        {
            settingsRoot = content.GetSettingsNode();
            if (settingsRoot == null)
            {
                return null;
            }
        }
        else
        {
            settingsRoot = settingsNode as Settings;
        }
        var socialSettings = settingsRoot?.Children(x => x.ContentType.Alias == SocialSettings.ModelTypeAlias)?.FirstOrDefault();
        return socialSettings?.As<SocialSettings>();
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

