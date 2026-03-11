namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// Response DTO returned by the allowed-blocks endpoint.
/// Contains the fully resolved restriction result for a content node,
/// including which block types are allowed and whether the restriction
/// was inherited from an ancestor in the content tree.
/// </summary>
public class AllowedBlocksResponse
{
    /// <summary>
    /// The alias of the document type where the restriction rule is defined.
    /// Empty when <see cref="HasRestrictions"/> is false.
    /// </summary>
    public string DocumentTypeAlias { get; set; } = string.Empty;

    /// <summary>
    /// The element type aliases that are allowed (human-readable, as stored in the database).
    /// </summary>
    public List<string> AllowedBlocks { get; set; } = [];

    /// <summary>
    /// The resolved GUIDs of the allowed content element types.
    /// These are resolved from <see cref="AllowedBlocks"/> at query time via IContentTypeService.
    /// The frontend property editors use these keys to filter block type configurations.
    /// </summary>
    public List<Guid> AllowedContentElementTypeKeys { get; set; } = [];

    /// <summary>
    /// True when a restriction rule was found (either directly or inherited).
    /// False means no restrictions exist at any level — fail-open, all blocks allowed.
    /// </summary>
    public bool HasRestrictions { get; set; }

    /// <summary>
    /// True when the restriction was inherited from an ancestor content node
    /// rather than being set directly on the current node's document type.
    /// Used by the frontend to show "inherited from {documentTypeAlias}" messaging.
    /// </summary>
    public bool InheritedFromAncestor { get; set; }
}
