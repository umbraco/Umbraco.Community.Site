namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// EF Core entity representing a single block restriction rule in the database.
/// Each row maps one document type to its list of allowed block element type aliases.
/// The table is created automatically by EF Core migrations on application startup.
/// </summary>
public class BlockRestrictionEntity
{
    /// <summary>Primary key, auto-incremented by the database.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The unique key (GUID) of the Umbraco document type this rule applies to.
    /// Has a unique index — only one rule per document type is allowed.
    /// </summary>
    public Guid DocumentTypeKey { get; set; }

    /// <summary>
    /// A JSON-serialized array of element type aliases that are allowed,
    /// e.g. <c>["heroBlock","gridBlock","ctaBlock"]</c>.
    /// Aliases are used rather than GUIDs for portability across environments.
    /// </summary>
    public string AllowedBlockAliasesJson { get; set; } = "[]";

    /// <summary>UTC timestamp of when this rule was first created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update to this rule.</summary>
    public DateTime UpdatedAt { get; set; }
}
