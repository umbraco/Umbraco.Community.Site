using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

public class BlockRestrictionStore
{
    private readonly IDbContextFactory<BlockRestrictionDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
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

    public async Task<List<BlockRestrictionEntity>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BlockRestrictionRules.ToListAsync();
    }

    private void InvalidateCache(Guid documentTypeKey)
    {
        _cache.Remove($"{CachePrefix}{documentTypeKey}");
    }
}
