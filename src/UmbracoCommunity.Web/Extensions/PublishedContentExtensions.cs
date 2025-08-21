using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Extensions;

public static class PublishedContentExtensions
{
    public static T As<T>(this IPublishedContent? content) where T : class, IPublishedContent =>
        content as T ?? throw new ArgumentException($"Provided published content is null or not composed of the expected content model: {typeof(T).FullName}. Content provided is {GetElementDescription(content)}.", nameof(content));

    private static string GetElementDescription(IPublishedContent? element)
    {
        if (element is null)
        {
            return "null";
        }

        return $" of content type alias: {element.ContentType.Alias}";
    }
}

