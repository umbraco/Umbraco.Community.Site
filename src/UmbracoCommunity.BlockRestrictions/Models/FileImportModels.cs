namespace UmbracoCommunity.BlockRestrictions.Models;

/// <summary>
/// Response from the file import preview endpoint. Categorises every rule
/// into one of: add, update, unchanged, delete, or unknown alias.
/// </summary>
public class FileImportPreviewResponse
{
    public List<FileImportRuleChange> ToAdd { get; set; } = [];
    public List<FileImportRuleChange> ToUpdate { get; set; } = [];
    public List<FileImportRuleChange> Unchanged { get; set; } = [];
    public List<FileImportOrphanedRule> ToDelete { get; set; } = [];
    public List<FileImportUnknownAlias> UnknownAliases { get; set; } = [];
    public bool HasChanges => ToAdd.Count > 0 || ToUpdate.Count > 0 || ToDelete.Count > 0;
}

/// <summary>
/// A rule diff: what the file says vs what the database has.
/// Used for ToAdd, ToUpdate, and Unchanged categories.
/// </summary>
public class FileImportRuleChange
{
    public string Alias { get; set; } = string.Empty;
    public Guid DocumentTypeKey { get; set; }
    public List<string> FileBlocks { get; set; } = [];
    public List<string> DbBlocks { get; set; } = [];
    public List<string> BlocksAdded { get; set; } = [];
    public List<string> BlocksRemoved { get; set; } = [];
}

/// <summary>
/// A database rule with no corresponding JSON file (will be deleted on apply).
/// </summary>
public class FileImportOrphanedRule
{
    public Guid DocumentTypeKey { get; set; }
    public string Alias { get; set; } = string.Empty;
    public List<string> CurrentBlocks { get; set; } = [];
}

/// <summary>
/// A JSON file referencing a document type alias that doesn't exist in Umbraco.
/// </summary>
public class FileImportUnknownAlias
{
    public string Alias { get; set; } = string.Empty;
}

/// <summary>
/// Response from the file import apply endpoint with operation counts.
/// </summary>
public class FileImportApplyResponse
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public List<FileImportApplyError> Errors { get; set; } = [];
}

/// <summary>
/// An error that occurred while applying a specific rule.
/// </summary>
public class FileImportApplyError
{
    public string Alias { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Response from the zip upload endpoint with count of files written and any errors.
/// </summary>
public class FileUploadResponse
{
    public int FilesWritten { get; set; }
    public List<FileImportApplyError> Errors { get; set; } = [];
}
