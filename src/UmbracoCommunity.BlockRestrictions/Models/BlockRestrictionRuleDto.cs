namespace UmbracoCommunity.BlockRestrictions.Models;

public class BlockRestrictionRuleDto
{
    public Guid DocumentTypeKey { get; set; }
    public List<string> AllowedBlockAliases { get; set; } = [];
}
