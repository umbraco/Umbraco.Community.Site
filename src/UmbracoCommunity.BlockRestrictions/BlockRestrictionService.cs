using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.BlockRestrictions.Infrastructure;
using UmbracoCommunity.BlockRestrictions.Models;

namespace UmbracoCommunity.BlockRestrictions;

/// <summary>
/// Core business logic for block restrictions.
/// Handles restriction resolution (walking the content tree), alias-to-key resolution,
/// rule persistence, and querying element types and data types for the workspace view.
///
/// This service sits between the API controller and the data store, adding caching,
/// content tree traversal, and Umbraco service integration.
/// </summary>
public class BlockRestrictionService
{
    private readonly BlockRestrictionStore _store;
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BlockRestrictionService> _logger;

    // --- Resolved-result cache ---
    // The store caches individual rules per document type key.
    // This second cache layer stores the *fully resolved* result for a content node,
    // avoiding the repeated tree walk + alias resolution on subsequent requests.
    private const string ResolvedCachePrefix = "BlockRestriction_Resolved_";

    /// <summary>
    /// Static generation counter included in cache keys. Incrementing this value
    /// effectively invalidates all cached resolved results without needing to enumerate
    /// and remove individual entries. Old entries expire naturally via TTL.
    /// Thread-safe via Interlocked operations.
    /// </summary>
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

    /// <summary>
    /// Resolves the effective block restrictions for a content node.
    /// Returns a cached result if available, otherwise performs the full tree walk.
    ///
    /// Called by the property editors every time a restricted block grid/list loads.
    /// A page with multiple restricted editors will only do the tree walk once per
    /// 60-second cache window.
    /// </summary>
    /// <returns>
    /// The resolved restrictions, or null if the content node was not found (404).
    /// When no restrictions exist at any level, returns HasRestrictions = false (fail-open).
    /// </returns>
    public async Task<AllowedBlocksResponse?> ResolveAllowedBlocksForNodeAsync(Guid nodeKey)
    {
        // Include the rule version in the cache key so that saving/deleting any rule
        // instantly invalidates all resolved results.
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

    /// <summary>
    /// Resolves block restrictions for new content that doesn't exist in the tree yet.
    /// Called as a fallback when the node key doesn't match an existing content node.
    ///
    /// Algorithm:
    ///   1. If contentTypeKey is provided, check for a direct rule on that document type
    ///   2. If no direct rule and parentKey is provided, walk up from the parent node
    ///   3. If neither yields a result, return fail-open (all blocks allowed)
    ///
    /// This enables restrictions to work during content creation, not just editing.
    /// </summary>
    public async Task<AllowedBlocksResponse?> ResolveForNewContentAsync(Guid? contentTypeKey, Guid? parentKey)
    {
        // Check the new content's document type for a direct rule.
        if (contentTypeKey.HasValue)
        {
            var rule = await _store.GetByDocumentTypeKeyAsync(contentTypeKey.Value);
            if (rule != null)
            {
                var contentType = _contentTypeService.Get(contentTypeKey.Value);
                var aliases = JsonSerializer.Deserialize<List<string>>(rule.AllowedBlockAliasesJson) ?? [];
                var elementTypeKeys = ResolveContentElementTypeKeys(aliases);

                return new AllowedBlocksResponse
                {
                    DocumentTypeAlias = contentType?.Alias ?? "",
                    AllowedBlocks = aliases,
                    AllowedContentElementTypeKeys = elementTypeKeys,
                    HasRestrictions = true,
                    InheritedFromAncestor = false
                };
            }
        }

        // No direct rule — walk up from the parent node to check for inherited restrictions.
        if (parentKey.HasValue)
        {
            return await ResolveAllowedBlocksForNodeAsync(parentKey.Value);
        }

        return null;
    }

    /// <summary>
    /// The actual tree-walk logic, called on cache miss.
    ///
    /// Algorithm:
    ///   1. Look up the content node by GUID
    ///   2. Get its document type and check for a restriction rule
    ///   3. If no rule, walk up the content tree via ParentId
    ///   4. At each ancestor, check its document type for a rule
    ///   5. First rule found wins — resolve aliases to GUIDs and return
    ///   6. If no rule found at any level, return fail-open (all blocks allowed)
    ///
    /// Note: this walks the content tree (parent nodes), NOT the document type
    /// inheritance chain. This means the same document type can have different
    /// restrictions depending on where in the tree it appears.
    /// </summary>
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
                // Found a rule — deserialize the stored aliases and resolve them to GUIDs.
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

            // No rule on this node's document type — walk up to the parent.
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

        // No restrictions found at any level — fail-open design.
        // The property editors will show all blocks (existing behaviour).
        return new AllowedBlocksResponse
        {
            HasRestrictions = false
        };
    }

    /// <summary>
    /// Gets the restriction rule for a specific document type (without tree walking).
    /// Used by the workspace view to load the current configuration for editing.
    /// Returns null if no rule exists (the document type inherits from ancestors).
    /// </summary>
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

    /// <summary>
    /// Resolves element type aliases to their GUIDs using a single batched query.
    /// Loads all element types once and builds a dictionary for O(1) lookup per alias.
    /// This replaces N individual Get(alias) calls with one GetAll() call.
    /// </summary>
    public List<Guid> ResolveContentElementTypeKeys(List<string> aliases)
    {
        // Build a set of the aliases we need to resolve for efficient Contains() checks.
        var aliasSet = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);

        // Single query: load all element types, filter to the ones we need, build lookup.
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
                // Alias doesn't resolve — the element type may have been renamed or deleted.
                _logger.LogWarning("Element type not found for alias: {Alias}", alias);
            }
        }
        return keys;
    }

    /// <summary>
    /// Saves a restriction rule for a document type and invalidates the resolved cache.
    /// </summary>
    public async Task SaveConfigAsync(Guid documentTypeKey, List<string> allowedAliases)
    {
        await _store.UpsertAsync(documentTypeKey, allowedAliases);

        // Increment the version counter to invalidate all cached resolved results.
        // This is necessary because changing a rule on one document type affects
        // all descendant content nodes that inherit from it.
        Interlocked.Increment(ref _ruleVersion);
    }

    /// <summary>
    /// Deletes a restriction rule for a document type (returning it to inheritance)
    /// and invalidates the resolved cache.
    /// </summary>
    public async Task<bool> DeleteConfigAsync(Guid documentTypeKey)
    {
        var deleted = await _store.DeleteAsync(documentTypeKey);
        if (deleted)
        {
            Interlocked.Increment(ref _ruleVersion);
        }
        return deleted;
    }

    /// <summary>
    /// Returns all element types (content types where IsElement = true) in Umbraco.
    /// Used by the workspace view to populate the block type checklist.
    /// </summary>
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

    /// <summary>
    /// Maps our restricted property editor UI aliases to friendly display names.
    /// Used to filter data types and label them in the workspace view dropdown.
    /// Only data types using these UI aliases appear in the "Filter by data type" dropdown.
    /// </summary>
    private static readonly Dictionary<string, string> RestrictedEditorUiAliases = new()
    {
        ["UmbracoCommunity.PropertyEditorUi.BlockGridRestricted"] = "Block Grid (Restricted)",
        ["UmbracoCommunity.PropertyEditorUi.BlockListRestricted"] = "Block List (Restricted)",
    };

    /// <summary>
    /// Returns all data types that use the restricted Block Grid or Block List property
    /// editor UIs, along with the content element type keys configured on each.
    /// Used by the workspace view's "Filter by data type" dropdown.
    ///
    /// The configuration is parsed from the data type's ConfigurationObject by serializing
    /// it to JSON and extracting the blocks[].contentElementTypeKey values.
    /// </summary>
    public async Task<List<BlockDataTypeInfo>> GetBlockDataTypes()
    {
        var result = new List<BlockDataTypeInfo>();
        var allDataTypes = await _dataTypeService.GetAllAsync();

        // Filter to only data types using our restricted property editor UIs.
        // We filter by EditorUiAlias (not EditorAlias) because the underlying schema
        // is the same as the native editors — only the UI wrapper is different.
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

            // Extract the content element type keys from the data type's block configuration.
            // The configuration object is an opaque type, so we serialize it to JSON
            // and parse the blocks array to find each block's contentElementTypeKey.
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

        // Sort by editor type first (Block Grid before Block List), then by name.
        return result.OrderBy(r => r.EditorType).ThenBy(r => r.Name).ToList();
    }
}
