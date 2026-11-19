using Umbraco.Cms.Search.Provider.Examine.Helpers;

namespace UmbracoCommunity.Web.Services;

/// <summary>
/// Field names and stored-value rules for Umbraco's published-content search index
/// (<c>Umb_PublishedContent</c>), which names each Lucene field <c>Field_{alias}_{kind}</c>.
/// </summary>
internal static class PublishedContentIndexFields
{
    // The provider's own suffix constants are internal, so these mirror them.
    internal const string Texts = "texts";
    internal const string TextsR1 = "textsr1";
    internal const string TextsR2 = "textsr2";
    internal const string TextsR3 = "textsr3";
    internal const string Keywords = "keywords";

    /// <summary>Stored access ids of the page; public pages hold only <see cref="Guid.Empty"/>.</summary>
    internal const string Protection = "Sys_Protection";

    internal static readonly string Name = FieldNameHelper.FieldName(Umbraco.Cms.Core.Constants.Search.FieldNames.Name, TextsR1);
    internal static readonly string Key = FieldNameHelper.FieldName(Umbraco.Cms.Core.Constants.Search.FieldNames.Id, Keywords);

    internal static string Field(string propertyAlias, string kind = Texts) => FieldNameHelper.FieldName(propertyAlias, kind);

    /// <summary>
    /// True when the stored protection ids mark the page as public. A page without the field counts as protected.
    /// </summary>
    internal static bool IsPublic(IEnumerable<string> protectionIds)
        => protectionIds.Any(id => Guid.TryParse(id, out var guid) && guid == Guid.Empty);

    /// <summary>
    /// Picks the first block text that reads like prose: block fields also hold single values such as
    /// file names, link hosts and button labels, which make poor excerpts.
    /// </summary>
    internal static string? FirstProse(IEnumerable<string> values)
        => values.FirstOrDefault(value => value.Trim().Contains(' '));
}
