namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// POCO matching the JSON file format for file-based block restriction persistence.
/// One file per document type is stored in umbraco/BlockRestrictions/{alias}.json.
/// </summary>
public class BlockRestrictionFileModel
{
    public string DocumentTypeAlias { get; set; } = string.Empty;
    public List<string> AllowedBlocks { get; set; } = [];
}
