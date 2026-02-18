namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// Request body for the PUT rules/{docTypeKey} endpoint.
/// Contains the list of element type aliases to allow for the document type.
/// An empty list effectively means "restrictions enabled but no blocks allowed".
/// </summary>
public class SaveBlockRestrictionRequest
{
    /// <summary>
    /// The element type aliases to allow. These are stored as-is in the database
    /// and resolved to GUIDs at query time.
    /// </summary>
    public List<string> AllowedBlockAliases { get; set; } = [];
}
