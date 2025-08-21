using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Extensions;

public static class PublishedElementExtensions
{
    public static T As<T>(this IPublishedElement? element) where T : class, IPublishedElement =>
        element as T ?? throw new ArgumentException($"Provided published element is null or not of the expected model type: {typeof(T).FullName}. Element provided is {GetElementDescription(element)}.", nameof(element));

    private static string GetElementDescription(IPublishedElement? element)
    {
        if (element is null)
        {
            return "null";
        }

        return $" of content type alias: {element.ContentType.Alias}";
    }
}
