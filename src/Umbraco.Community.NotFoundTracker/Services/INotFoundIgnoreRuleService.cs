using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public interface INotFoundIgnoreRuleService
{
    Task<IReadOnlyList<NotFoundIgnoreRuleEntity>> ListAsync(UserScope scope, CancellationToken ct);
    Task<IgnoreRuleMutation> CreateAsync(CreateIgnoreRuleInput input, UserScope scope, CancellationToken ct);
    Task<IgnoreRuleMutation> UpdateAsync(int id, UpdateIgnoreRuleInput input, UserScope scope, CancellationToken ct);
    Task<IgnoreRuleMutation> DeleteAsync(int id, UserScope scope, CancellationToken ct);
}

public sealed record CreateIgnoreRuleInput(
    string Path,
    IgnoreMatchType MatchType,
    string? Hostname,
    string? Note);

public sealed record UpdateIgnoreRuleInput(
    string Path,
    IgnoreMatchType MatchType,
    string? Hostname,
    string? Note);

public enum IgnoreRuleMutationResult
{
    Ok,
    NotFound,
    Forbidden,
    Conflict,
    InvalidInput,
}

public sealed record IgnoreRuleMutation(
    IgnoreRuleMutationResult Result,
    NotFoundIgnoreRuleEntity? Entity = null,
    string? Reason = null);
