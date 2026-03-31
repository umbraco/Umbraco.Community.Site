using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoCommunity.BlockRestrictions.Models;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// Handles read/write/delete of block restriction rule JSON files in umbraco/BlockRestrictions/.
/// Files are the version-controlled source of truth — they travel with the codebase and are
/// imported into the database on startup by <see cref="BlockRestrictionFileImportHostedService"/>.
///
/// Registered as a singleton because it only depends on the content root path (immutable)
/// and a logger. Thread safety is not critical — concurrent writes to different files are
/// safe, and same-file races are unlikely in practice (backoffice saves are user-initiated).
/// </summary>
public class BlockRestrictionFileService
{
    private readonly string _directory;
    private readonly ILogger<BlockRestrictionFileService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public BlockRestrictionFileService(
        IWebHostEnvironment hostEnvironment,
        ILogger<BlockRestrictionFileService> logger)
    {
        _directory = Path.Combine(hostEnvironment.ContentRootPath, "umbraco", "BlockRestrictions");
        _logger = logger;
    }

    /// <summary>
    /// Writes a rule file for the given document type alias.
    /// Creates the directory if it doesn't exist. Aliases are sorted alphabetically
    /// for deterministic git diffs.
    /// </summary>
    public void SaveRuleFile(string documentTypeAlias, List<string> allowedAliases)
    {
        try
        {
            Directory.CreateDirectory(_directory);

            var model = new BlockRestrictionFileModel
            {
                DocumentTypeAlias = documentTypeAlias,
                AllowedBlocks = allowedAliases.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList()
            };

            var json = JsonSerializer.Serialize(model, JsonOptions);
            var filePath = Path.Combine(_directory, $"{documentTypeAlias}.json");
            File.WriteAllText(filePath, json);

            _logger.LogInformation("Saved block restriction file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save block restriction file for {Alias}", documentTypeAlias);
        }
    }

    /// <summary>
    /// Deletes the rule file for the given document type alias. No-op if the file doesn't exist.
    /// </summary>
    public void DeleteRuleFile(string documentTypeAlias)
    {
        try
        {
            var filePath = Path.Combine(_directory, $"{documentTypeAlias}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted block restriction file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete block restriction file for {Alias}", documentTypeAlias);
        }
    }

    /// <summary>
    /// Reads and deserializes all .json files in the BlockRestrictions directory.
    /// Returns an empty list if the directory doesn't exist.
    /// Skips files that fail to deserialize (logs a warning).
    /// </summary>
    public List<BlockRestrictionFileModel> ReadAllRuleFiles()
    {
        var results = new List<BlockRestrictionFileModel>();

        if (!Directory.Exists(_directory))
        {
            return results;
        }

        foreach (var filePath in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var model = JsonSerializer.Deserialize<BlockRestrictionFileModel>(json);
                if (model != null && !string.IsNullOrWhiteSpace(model.DocumentTypeAlias))
                {
                    results.Add(model);
                }
                else
                {
                    _logger.LogWarning("Skipping invalid block restriction file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read block restriction file: {FilePath}", filePath);
            }
        }

        return results;
    }
}
