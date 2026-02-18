namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// DTO representing an Umbraco element type (content type where IsElement = true).
/// Used by the workspace view to render the block type checklist with icons and aliases.
/// </summary>
public class ElementTypeInfo
{
    /// <summary>The unique key (GUID) of the element type.</summary>
    public Guid Key { get; set; }

    /// <summary>The alias of the element type, e.g. "heroBlock".</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name of the element type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The icon alias for display in the backoffice, e.g. "icon-document".</summary>
    public string Icon { get; set; } = string.Empty;
}
