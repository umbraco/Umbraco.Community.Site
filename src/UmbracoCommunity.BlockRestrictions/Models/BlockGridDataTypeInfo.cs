namespace UmbracoCommunity.BlockRestrictions.Models;

public class BlockDataTypeInfo
{
    public Guid Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EditorType { get; set; } = string.Empty;
    public List<Guid> ContentElementTypeKeys { get; set; } = [];
}
