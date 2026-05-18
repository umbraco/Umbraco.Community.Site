using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundIgnoreRuleService : INotFoundIgnoreRuleService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly INotFoundIgnoreRuleMatcher _matcher;

    public NotFoundIgnoreRuleService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        INotFoundIgnoreRuleMatcher matcher)
    {
        _contextFactory = contextFactory;
        _matcher = matcher;
    }

    public async Task<IReadOnlyList<NotFoundIgnoreRuleEntity>> ListAsync(UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rules = await context.NotFoundIgnoreRules.AsNoTracking().ToListAsync(ct);

        if (scope.HasFullAccess) return rules;

        return rules
            .Where(r => r.Hostname is null || scope.AccessibleHostnames.Contains(r.Hostname))
            .ToList();
    }

    public async Task<IgnoreRuleMutation> CreateAsync(CreateIgnoreRuleInput input, UserScope scope, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.Path))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.InvalidInput, Reason: "Path is required.");
        }

        if (input.Hostname is null)
        {
            if (!scope.HasFullAccess)
            {
                return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                    Reason: "Creating a global ignore rule requires full content access.");
            }
        }
        else if (!scope.CanAccessHostname(input.Hostname))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                Reason: $"User does not have access to hostname '{input.Hostname}'.");
        }

        var normalizedPath = UrlNormalizer.NormalizePath(input.Path);
        var normalizedHostname = string.IsNullOrEmpty(input.Hostname) ? null : UrlNormalizer.NormalizeHostname(input.Hostname);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var duplicate = await context.NotFoundIgnoreRules.AnyAsync(
            r => r.Hostname == normalizedHostname && r.MatchType == input.MatchType && r.Path == normalizedPath, ct);
        if (duplicate)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Conflict,
                Reason: "An ignore rule with this hostname, match type, and path already exists.");
        }

        var entity = new NotFoundIgnoreRuleEntity
        {
            Hostname = normalizedHostname,
            MatchType = input.MatchType,
            Path = normalizedPath,
            Source = IgnoreRuleSource.UserDefined,
            Note = input.Note,
            CreatedUtc = DateTime.UtcNow,
        };
        context.NotFoundIgnoreRules.Add(entity);
        await context.SaveChangesAsync(ct);

        await _matcher.RefreshAsync(ct);
        return new IgnoreRuleMutation(IgnoreRuleMutationResult.Ok, entity);
    }

    public async Task<IgnoreRuleMutation> UpdateAsync(int id, UpdateIgnoreRuleInput input, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.NotFoundIgnoreRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return new IgnoreRuleMutation(IgnoreRuleMutationResult.NotFound);

        if (entity.Source == IgnoreRuleSource.ConfigSeeded)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                Reason: "ConfigSeeded rules are read-only — edit them via appsettings.json.");
        }

        if (!CanMutate(entity.Hostname, scope) || !CanMutate(input.Hostname, scope))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden);
        }

        entity.Path = UrlNormalizer.NormalizePath(input.Path);
        entity.MatchType = input.MatchType;
        entity.Hostname = string.IsNullOrEmpty(input.Hostname) ? null : UrlNormalizer.NormalizeHostname(input.Hostname);
        entity.Note = input.Note;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Conflict);
        }

        await _matcher.RefreshAsync(ct);
        return new IgnoreRuleMutation(IgnoreRuleMutationResult.Ok, entity);
    }

    public async Task<IgnoreRuleMutation> DeleteAsync(int id, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.NotFoundIgnoreRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return new IgnoreRuleMutation(IgnoreRuleMutationResult.NotFound);

        if (entity.Source == IgnoreRuleSource.ConfigSeeded)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                Reason: "ConfigSeeded rules are read-only — remove them from appsettings.json.");
        }

        if (!CanMutate(entity.Hostname, scope))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden);
        }

        context.NotFoundIgnoreRules.Remove(entity);
        await context.SaveChangesAsync(ct);
        await _matcher.RefreshAsync(ct);
        return new IgnoreRuleMutation(IgnoreRuleMutationResult.Ok);
    }

    private static bool CanMutate(string? hostname, UserScope scope)
    {
        if (hostname is null) return scope.HasFullAccess;
        return scope.CanAccessHostname(hostname);
    }
}
