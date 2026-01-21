namespace UmbracoCommunity.Web.Features.Sessionize.Models;

/// <summary>
/// A simple data item format compatible with Contentment's JSON data source.
/// </summary>
public class ContentmentDataItem
{
    /// <summary>
    /// The display name shown to editors in the backoffice.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The value stored when this item is selected.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
