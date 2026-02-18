namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

public class BlockRestrictionEntity
{
    public int Id { get; set; }
    public Guid DocumentTypeKey { get; set; }
    public string AllowedBlockAliasesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
