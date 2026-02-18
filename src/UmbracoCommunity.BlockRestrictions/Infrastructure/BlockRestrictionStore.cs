using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// Data access layer for block restriction rules.
/// Wraps EF Core operations with an in-memory cache to reduce database round-trips
/// during content tree walks (where each ancestor's document type is checked for a rule).
///
/// Cache strategy: 30-minute sliding expiration per document type key.
/// Invalidated on upsert and delete — the service layer also maintains a separate
/// generation counter for its own resolved-result cache.
/// </summary>
public class BlockRestrictionStore
{
    private readonly IDbContextFactory<BlockRestrictionDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    /// <summary>Prefix for cache keys to avoid collisions with other IMemoryCache consumers.</summary>
    private const string CachePrefix = "BlockRestriction_";

    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    public BlockRestrictionStore(
        IDbContextFactory<BlockRestrictionDbContext> contextFactory,
        IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    /// <summary>
    /// Gets the restriction rule for a specific document type, using the cache first.
    /// Returns null if no rule exists (meaning this document type has no direct restriction).
    /// </summary>
    public async Task<BlockRestrictionEntity?> GetByDocumentTypeKeyAsync(Guid documentTypeKey)
    {
        var cacheKey = $"{CachePrefix}{documentTypeKey}";
        if (_cache.TryGetValue(cacheKey, out BlockRestrictionEntity? cached))
        {
            return cached;
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.BlockRestrictionRules
            .FirstOrDefaultAsync(r => r.DocumentTypeKey == documentTypeKey);

        if (entity != null)
        {
            _cache.Set(cacheKey, entity, CacheOptions);
        }

        return entity;
    }

    /// <summary>
    /// Creates or updates a restriction rule for a document type.
    /// If a rule already exists, updates the allowed aliases and timestamp.
    /// Invalidates the cache entry for this document type.
    /// </summary>
    public async Task UpsertAsync(Guid documentTypeKey, List<string> allowedAliases)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.BlockRestrictionRules
            .FirstOrDefaultAsync(r => r.DocumentTypeKey == documentTypeKey);

        var json = JsonSerializer.Serialize(allowedAliases);
        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new BlockRestrictionEntity
            {
                DocumentTypeKey = documentTypeKey,
                AllowedBlockAliasesJson = json,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.BlockRestrictionRules.Add(entity);
        }
        else
        {
            entity.AllowedBlockAliasesJson = json;
            entity.UpdatedAt = now;
        }

        await context.SaveChangesAsync();
        InvalidateCache(documentTypeKey);
    }

    /// <summary>
    /// Deletes the restriction rule for a document type.
    /// Returns true if a rule was found and deleted, false if no rule existed.
    /// Invalidates the cache entry for this document type.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid documentTypeKey)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.BlockRestrictionRules
            .FirstOrDefaultAsync(r => r.DocumentTypeKey == documentTypeKey);

        if (entity == null) return false;

        context.BlockRestrictionRules.Remove(entity);
        await context.SaveChangesAsync();
        InvalidateCache(documentTypeKey);
        return true;
    }

    /// <summary>
    /// Gets all restriction rules. Used for potential future audit/dashboard features.
    /// Not cached — intended for infrequent administrative use.
    /// </summary>
    public async Task<List<BlockRestrictionEntity>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BlockRestrictionRules.ToListAsync();
    }

    /// <summary>Removes the cached rule for a document type after a write operation.</summary>
    private void InvalidateCache(Guid documentTypeKey)
    {
        _cache.Remove($"{CachePrefix}{documentTypeKey}");
    }
}
