namespace UmbracoCommunity.BlockRestrictions.Models;

public class AllowedBlocksResponse
{
    public string DocumentTypeAlias { get; set; } = string.Empty;
    public List<string> AllowedBlocks { get; set; } = [];
    public List<Guid> AllowedContentElementTypeKeys { get; set; } = [];
    public bool HasRestrictions { get; set; }
    public bool InheritedFromAncestor { get; set; }
}
