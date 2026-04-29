using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

/// <summary>
/// Minimal IPublishedContent stub. Only Key, Name, and the calendar feed properties (feedUrl,
/// cacheDurationMinutes) are populated. Anything else throws — tests will surface unintended
/// dependencies on Umbraco internals.
/// </summary>
internal sealed class StubPublishedContent : IPublishedContent
{
    public required Guid Key { get; init; }
    public required string FeedUrl { get; init; }
    public required int CacheDurationMinutes { get; init; }

    public string Name => "Stub feed";

    public T Value<T>(string alias, string? culture = null, string? segment = null,
        Fallback fallback = default, T? defaultValue = default)
    {
        if (alias == "feedUrl" && typeof(T) == typeof(string))
            return (T)(object)FeedUrl;
        if (alias == "cacheDurationMinutes" && typeof(T) == typeof(int))
            return (T)(object)CacheDurationMinutes;
        return defaultValue!;
    }

    public object? Value(string alias, string? culture = null, string? segment = null,
        Fallback fallback = default, object? defaultValue = null)
        => Value<object?>(alias, culture, segment, fallback, defaultValue);

    // The remainder of IPublishedContent is unused by CalendarFeedService.
    public int Id => throw new NotSupportedException();
    public string UrlSegment => throw new NotSupportedException();
    public int SortOrder => 0;
    public int Level => 0;
    public string Path => string.Empty;
    public int? TemplateId => null;
    public int CreatorId => 0;
    public DateTime CreateDate => default;
    public int WriterId => 0;
    public DateTime UpdateDate => default;
    public IReadOnlyDictionary<string, PublishedCultureInfo> Cultures => new Dictionary<string, PublishedCultureInfo>();
    public PublishedItemType ItemType => PublishedItemType.Content;
    public bool IsDraft(string? culture = null) => false;
    public bool IsPublished(string? culture = null) => true;
    public IPublishedContent? Parent => null;
    public IEnumerable<IPublishedContent> Children => Array.Empty<IPublishedContent>();
    public IEnumerable<IPublishedContent> ChildrenForAllCultures => Array.Empty<IPublishedContent>();
    public IPublishedContentType ContentType => throw new NotSupportedException();
    public IEnumerable<IPublishedProperty> Properties => Array.Empty<IPublishedProperty>();

    public IPublishedProperty? GetProperty(string alias) => alias switch
    {
        "feedUrl" => new StubPublishedProperty(alias, FeedUrl),
        "cacheDurationMinutes" => new StubPublishedProperty(alias, CacheDurationMinutes),
        _ => null,
    };
}

internal sealed class StubPublishedProperty : IPublishedProperty
{
    private readonly object? _value;

    public StubPublishedProperty(string alias, object? value)
    {
        Alias = alias;
        _value = value;
    }

    public string Alias { get; }
    public IPublishedPropertyType PropertyType => throw new NotSupportedException();

    public bool HasValue(string? culture = null, string? segment = null) => _value is not null;
    public object? GetSourceValue(string? culture = null, string? segment = null) => _value;
    public object? GetValue(string? culture = null, string? segment = null) => _value;
    public object? GetDeliveryApiValue(bool expanding, string? culture = null, string? segment = null) => _value;
}
