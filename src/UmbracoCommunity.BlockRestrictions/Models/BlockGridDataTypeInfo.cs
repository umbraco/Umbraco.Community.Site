namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// DTO representing a restricted Block Grid or Block List data type.
/// Used by the workspace view's "Filter by data type" dropdown to let
/// developers narrow the block type checklist to only show element types
/// that are configured on a specific data type.
/// </summary>
public class BlockDataTypeInfo
{
    /// <summary>The unique key (GUID) of the data type.</summary>
    public Guid Key { get; set; }

    /// <summary>The display name of the data type, e.g. "Homepage Block Grid".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The friendly editor type label, e.g. "Block Grid (Restricted)" or "Block List (Restricted)".
    /// Used for grouping/sorting in the dropdown.
    /// </summary>
    public string EditorType { get; set; } = string.Empty;

    /// <summary>
    /// The GUIDs of all content element types configured as blocks on this data type.
    /// Extracted from the data type's configuration at query time.
    /// </summary>
    public List<Guid> ContentElementTypeKeys { get; set; } = [];
}
