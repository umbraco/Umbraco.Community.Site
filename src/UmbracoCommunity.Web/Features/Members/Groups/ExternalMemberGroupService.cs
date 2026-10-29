using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace UmbracoCommunity.Web.Features.Members.Groups;

/// <summary>
/// Reads and changes the member groups of lightweight external-only members (the kind GitHub
/// sign-in creates). Umbraco 18.3's backoffice shows these groups read-only and its Management API
/// refuses to update external-only members, so the backoffice extension calls this instead.
/// </summary>
/// <remarks>
/// <see cref="IExternalMemberService"/> works with group <em>names</em> and silently ignores names
/// that don't exist, so this service takes group keys (what the backoffice member group picker
/// returns), resolves them first and rejects unknown ones. Members that are not external-only
/// (content-based members) are reported as not found: those are edited with the normal member editor.
/// </remarks>
public sealed class ExternalMemberGroupService
{
    private readonly IExternalMemberService _externalMemberService;
    private readonly IMemberGroupService _memberGroupService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly ILogger<ExternalMemberGroupService> _logger;

    public ExternalMemberGroupService(
        IExternalMemberService externalMemberService,
        IMemberGroupService memberGroupService,
        ICoreScopeProvider scopeProvider,
        ILogger<ExternalMemberGroupService> logger)
    {
        _externalMemberService = externalMemberService;
        _memberGroupService = memberGroupService;
        _scopeProvider = scopeProvider;
        _logger = logger;
    }

    /// <summary>Returns the member's groups, or <c>null</c> when there is no external-only member with this key.</summary>
    public async Task<ExternalMemberGroups?> GetAsync(Guid memberKey)
    {
        var member = await _externalMemberService.GetByKeyAsync(memberKey);
        if (member == null)
        {
            return null;
        }

        var roleNames = await _externalMemberService.GetRolesAsync(memberKey);
        var allGroups = await _memberGroupService.GetAllAsync();
        return new ExternalMemberGroups(memberKey, member.UserName, ToGroups(roleNames, allGroups));
    }

    /// <summary>
    /// Makes <paramref name="groupKeys"/> the member's complete set of groups: groups not in the
    /// set are removed, missing ones are added, and both happen in one transaction.
    /// </summary>
    /// <param name="memberKey">Key of the external-only member.</param>
    /// <param name="groupKeys">Keys of every group the member should be in afterwards (may be empty).</param>
    /// <param name="changedBy">Who made the change, for the log.</param>
    public async Task<ExternalMemberGroupsUpdateResult> SetAsync(
        Guid memberKey,
        IReadOnlyCollection<Guid> groupKeys,
        string changedBy)
    {
        var member = await _externalMemberService.GetByKeyAsync(memberKey);
        if (member == null)
        {
            return ExternalMemberGroupsUpdateResult.Fail(ExternalMemberGroupsUpdateStatus.MemberNotFound);
        }

        var requestedKeys = groupKeys.Distinct().ToArray();
        var desiredGroups = requestedKeys.Length == 0
            ? []
            : (await _memberGroupService.GetAsync(requestedKeys)).ToArray();

        var unknownKeys = requestedKeys.Except(desiredGroups.Select(g => g.Key)).ToArray();
        if (unknownKeys.Length > 0)
        {
            return ExternalMemberGroupsUpdateResult.Fail(ExternalMemberGroupsUpdateStatus.UnknownGroups, unknownKeys);
        }

        var desiredNames = desiredGroups.Select(g => g.Name).OfType<string>().ToArray();
        var currentNames = (await _externalMemberService.GetRolesAsync(memberKey)).ToArray();

        var toAdd = desiredNames.Except(currentNames, StringComparer.OrdinalIgnoreCase).ToArray();
        var toRemove = currentNames.Except(desiredNames, StringComparer.OrdinalIgnoreCase).ToArray();

        if (toAdd.Length > 0 || toRemove.Length > 0)
        {
            var failure = await ApplyAsync(memberKey, toAdd, toRemove);
            if (failure != null)
            {
                return failure;
            }

            _logger.LogInformation(
                "{ChangedBy} changed the member groups of external member {MemberKey} ({UserName}): added [{Added}], removed [{Removed}]",
                changedBy,
                memberKey,
                member.UserName,
                string.Join(", ", toAdd),
                string.Join(", ", toRemove));
        }

        var groups = await GetAsync(memberKey);
        return groups == null
            ? ExternalMemberGroupsUpdateResult.Fail(ExternalMemberGroupsUpdateStatus.MemberNotFound)
            : new ExternalMemberGroupsUpdateResult(ExternalMemberGroupsUpdateStatus.Success, groups, [], toAdd, toRemove);
    }

    // Both changes share one scope (transaction): if either fails, the scope is disposed without
    // being completed and nothing is changed. Returns null on success.
    private async Task<ExternalMemberGroupsUpdateResult?> ApplyAsync(Guid memberKey, string[] toAdd, string[] toRemove)
    {
        using var scope = _scopeProvider.CreateCoreScope();

        if (toRemove.Length > 0)
        {
            var removed = await _externalMemberService.RemoveRolesAsync(memberKey, toRemove);
            if (!removed.Success)
            {
                return Failed(memberKey, removed.Status);
            }
        }

        if (toAdd.Length > 0)
        {
            var assigned = await _externalMemberService.AssignRolesAsync(memberKey, toAdd);
            if (!assigned.Success)
            {
                return Failed(memberKey, assigned.Status);
            }
        }

        scope.Complete();
        return null;
    }

    private ExternalMemberGroupsUpdateResult Failed(Guid memberKey, ExternalMemberOperationStatus status)
    {
        _logger.LogWarning(
            "Changing the member groups of external member {MemberKey} failed with {Status}; nothing was changed",
            memberKey,
            status);

        return ExternalMemberGroupsUpdateResult.Fail(ExternalMemberGroupsUpdateStatus.Failed);
    }

    private static IReadOnlyList<ExternalMemberGroup> ToGroups(IEnumerable<string> roleNames, IEnumerable<IMemberGroup> allGroups)
    {
        var groupsByName = allGroups
            .Where(g => g.Name != null)
            .GroupBy(g => g.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return roleNames
            .Select(name => groupsByName.TryGetValue(name, out var group) ? new ExternalMemberGroup(group.Key, group.Name!) : null)
            .OfType<ExternalMemberGroup>()
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

/// <summary>A member group as shown to the backoffice.</summary>
public sealed record ExternalMemberGroup(Guid Key, string Name);

/// <summary>An external-only member and the groups they are in.</summary>
public sealed record ExternalMemberGroups(Guid MemberKey, string UserName, IReadOnlyList<ExternalMemberGroup> Groups);

public enum ExternalMemberGroupsUpdateStatus
{
    Success,
    MemberNotFound,
    UnknownGroups,
    Failed,
}

public sealed record ExternalMemberGroupsUpdateResult(
    ExternalMemberGroupsUpdateStatus Status,
    ExternalMemberGroups? Groups,
    IReadOnlyList<Guid> UnknownGroupKeys,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed)
{
    internal static ExternalMemberGroupsUpdateResult Fail(ExternalMemberGroupsUpdateStatus status, IReadOnlyList<Guid>? unknownGroupKeys = null) =>
        new(status, null, unknownGroupKeys ?? [], [], []);
}
