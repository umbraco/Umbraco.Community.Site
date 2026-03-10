namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// DTO representing a block restriction rule for a single document type.
/// Returned by the GET rules/{docTypeKey} endpoint and used by the
/// workspace view to populate the block type checklist on load.
/// </summary>
public class BlockRestrictionRuleDto
{
    /// <summary>
    /// The unique key (GUID) of the document type this rule applies to.
    /// </summary>
    public Guid DocumentTypeKey { get; set; }

    /// <summary>
    /// The list of element type aliases that are allowed for this document type.
    /// Aliases are stored rather than GUIDs to keep rules human-readable and
    /// portable across environments where GUIDs may differ.
    /// </summary>
    public List<string> AllowedBlockAliases { get; set; } = [];
}
