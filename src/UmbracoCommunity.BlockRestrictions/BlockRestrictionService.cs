using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.BlockRestrictions.Infrastructure;
using UmbracoCommunity.BlockRestrictions.Models;

namespace UmbracoCommunity.BlockRestrictions;

public class BlockRestrictionService
{
    private readonly BlockRestrictionStore _store;
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BlockRestrictionService> _logger;

    private const string ResolvedCachePrefix = "BlockRestriction_Resolved_";
    private static long _ruleVersion;
    private static readonly MemoryCacheEntryOptions ResolvedCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
    };

    public BlockRestrictionService(
        BlockRestrictionStore store,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IMemoryCache cache,
        ILogger<BlockRestrictionService> logger)
    {
        _store = store;
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AllowedBlocksResponse?> ResolveAllowedBlocksForNodeAsync(Guid nodeKey)
    {
        var version = Interlocked.Read(ref _ruleVersion);
        var cacheKey = $"{ResolvedCachePrefix}{version}_{nodeKey}";

        if (_cache.TryGetValue(cacheKey, out AllowedBlocksResponse? cached))
        {
            return cached;
        }

        var result = await ResolveAllowedBlocksForNodeCoreAsync(nodeKey);

        if (result != null)
        {
            _cache.Set(cacheKey, result, ResolvedCacheOptions);
        }

        return result;
    }

    private async Task<AllowedBlocksResponse?> ResolveAllowedBlocksForNodeCoreAsync(Guid nodeKey)
    {
        var content = _contentService.GetById(nodeKey);
        if (content == null)
        {
            _logger.LogWarning("Content node not found: {NodeKey}", nodeKey);
            return null;
        }

        var currentContent = content;
        var isInherited = false;

        while (currentContent != null)
        {
            var contentType = _contentTypeService.Get(currentContent.ContentTypeId);
            if (contentType == null) break;

            var rule = await _store.GetByDocumentTypeKeyAsync(contentType.Key);
            if (rule != null)
            {
                var aliases = JsonSerializer.Deserialize<List<string>>(rule.AllowedBlockAliasesJson) ?? [];
                var elementTypeKeys = ResolveContentElementTypeKeys(aliases);

                return new AllowedBlocksResponse
                {
                    DocumentTypeAlias = contentType.Alias,
                    AllowedBlocks = aliases,
                    AllowedContentElementTypeKeys = elementTypeKeys,
                    HasRestrictions = true,
                    InheritedFromAncestor = isInherited
                };
            }

            // Walk up the tree
            if (currentContent.ParentId > 0)
            {
                currentContent = _contentService.GetById(currentContent.ParentId);
                isInherited = true;
            }
            else
            {
                break;
            }
        }

        // No restrictions found at any level - fail-open
        return new AllowedBlocksResponse
        {
            HasRestrictions = false
        };
    }

    public async Task<BlockRestrictionRuleDto?> GetConfigForDocumentTypeAsync(Guid documentTypeKey)
    {
        var rule = await _store.GetByDocumentTypeKeyAsync(documentTypeKey);
        if (rule == null) return null;

        var aliases = JsonSerializer.Deserialize<List<string>>(rule.AllowedBlockAliasesJson) ?? [];
        return new BlockRestrictionRuleDto
        {
            DocumentTypeKey = documentTypeKey,
            AllowedBlockAliases = aliases
        };
    }

    public List<Guid> ResolveContentElementTypeKeys(List<string> aliases)
    {
        var aliasSet = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);
        var lookup = _contentTypeService.GetAll()
            .Where(ct => ct.IsElement && aliasSet.Contains(ct.Alias))
            .ToDictionary(ct => ct.Alias, ct => ct.Key, StringComparer.OrdinalIgnoreCase);

        var keys = new List<Guid>(aliases.Count);
        foreach (var alias in aliases)
        {
            if (lookup.TryGetValue(alias, out var key))
            {
                keys.Add(key);
            }
            else
            {
                _logger.LogWarning("Element type not found for alias: {Alias}", alias);
            }
        }
        return keys;
    }

    public async Task SaveConfigAsync(Guid documentTypeKey, List<string> allowedAliases)
    {
        await _store.UpsertAsync(documentTypeKey, allowedAliases);
        Interlocked.Increment(ref _ruleVersion);
    }

    public async Task<bool> DeleteConfigAsync(Guid documentTypeKey)
    {
        var deleted = await _store.DeleteAsync(documentTypeKey);
        if (deleted)
        {
            Interlocked.Increment(ref _ruleVersion);
        }
        return deleted;
    }

    public List<ElementTypeInfo> GetAllElementTypes()
    {
        return _contentTypeService.GetAll()
            .Where(ct => ct.IsElement)
            .Select(ct => new ElementTypeInfo
            {
                Key = ct.Key,
                Alias = ct.Alias,
                Name = ct.Name ?? ct.Alias,
                Icon = ct.Icon ?? "icon-document"
            })
            .OrderBy(e => e.Name)
            .ToList();
    }

    private static readonly Dictionary<string, string> RestrictedEditorUiAliases = new()
    {
        ["UmbracoCommunity.PropertyEditorUi.BlockGridRestricted"] = "Block Grid (Restricted)",
        ["UmbracoCommunity.PropertyEditorUi.BlockListRestricted"] = "Block List (Restricted)",
    };

    public async Task<List<BlockDataTypeInfo>> GetBlockDataTypes()
    {
        var result = new List<BlockDataTypeInfo>();
        var allDataTypes = await _dataTypeService.GetAllAsync();
        var blockTypes = allDataTypes
            .Where(dt => dt.EditorUiAlias != null && RestrictedEditorUiAliases.ContainsKey(dt.EditorUiAlias));

        foreach (var dt in blockTypes)
        {
            var info = new BlockDataTypeInfo
            {
                Key = dt.Key,
                Name = dt.Name ?? "Unnamed",
                EditorType = RestrictedEditorUiAliases[dt.EditorUiAlias!]
            };

            if (dt.ConfigurationObject != null)
            {
                try
                {
                    var json = JsonSerializer.Serialize(dt.ConfigurationObject,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("blocks", out var blocks))
                    {
                        foreach (var block in blocks.EnumerateArray())
                        {
                            if (block.TryGetProperty("contentElementTypeKey", out var key)
                                && Guid.TryParse(key.GetString(), out var guid))
                            {
                                info.ContentElementTypeKeys.Add(guid);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse block configuration for {DataTypeName}", dt.Name);
                }
            }

            result.Add(info);
        }

        return result.OrderBy(r => r.EditorType).ThenBy(r => r.Name).ToList();
    }
}
