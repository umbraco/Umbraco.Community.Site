using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>
/// Converts existing content-based community members (member type <c>communityMember</c>) into
/// Umbraco's lightweight external-only members, keeping each member's <see cref="IMember.Key"/>
/// so everything keyed by it (MemberProfileStore rows, uploaded avatars, the GitHub login link)
/// keeps working. <see cref="ExternalMemberMigrationStartupJob"/> runs <see cref="MigrateAsync"/> shortly
/// after startup (switch off with <c>ExternalMemberMigration:RunOnStartup</c>); the backoffice API can also
/// trigger it, and <see cref="PlanAsync"/> reports what it would do without changing anything.
/// </summary>
/// <remarks>
/// <para>
/// Per member, <see cref="MigrateAsync"/>:
/// </para>
/// <list type="number">
/// <item>Snapshots the identity fields, member groups and external logins/tokens.</item>
/// <item>In one transaction, deletes the content member and creates the external member with the
/// same key and groups. If creating fails, the transaction rolls back and the content member stays.</item>
/// <item>After that transaction commits, re-saves the external logins and tokens against the key.
/// This has to be a second step: deleting a member deletes its login rows, and Umbraco's
/// <c>DeleteExternalLoginsOnMemberDeletedHandler</c> runs again <em>after</em> the commit (scoped
/// notifications are published on scope exit), so logins saved inside the first transaction would
/// be wiped.</item>
/// </list>
/// <para>
/// If step 3 fails the member exists but has no GitHub link. That heals itself on their next
/// sign-in as long as their GitHub primary email is unchanged (auto-linking finds the external
/// member by email and re-adds the link); the snapshot is logged as an error so it can also be
/// repaired by hand.
/// </para>
/// <para>
/// Re-running is safe: converted members are no longer content members, so they aren't picked up again.
/// Only one run happens at a time: per instance via a semaphore, across load-balanced instances via
/// <see cref="ExternalMemberMigrationLease"/>.
/// </para>
/// </remarks>
public sealed class ExternalMemberMigrationService
{
    // One run at a time per instance; two overlapping runs would race on the same members.
    private static readonly SemaphoreSlim RunLock = new(1, 1);

    private readonly IMemberService _memberService;
    private readonly IExternalMemberService _externalMemberService;
    private readonly IExternalLoginWithKeyService _externalLoginService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly ExternalMemberMigrationLease _lease;
    private readonly ILogger<ExternalMemberMigrationService> _logger;

    public ExternalMemberMigrationService(
        IMemberService memberService,
        IExternalMemberService externalMemberService,
        IExternalLoginWithKeyService externalLoginService,
        ICoreScopeProvider scopeProvider,
        ExternalMemberMigrationLease lease,
        ILogger<ExternalMemberMigrationService> logger)
    {
        _memberService = memberService;
        _externalMemberService = externalMemberService;
        _externalLoginService = externalLoginService;
        _scopeProvider = scopeProvider;
        _lease = lease;
        _logger = logger;
    }

    /// <summary>Dry run: reports what <see cref="MigrateAsync"/> would do, changing nothing.</summary>
    public async Task<ExternalMemberMigrationReport> PlanAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<ExternalMemberMigrationItem>();
        foreach (var member in GetContentMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = Snapshot(member);
            var skipReason = await GetSkipReasonAsync(member, snapshot);
            items.Add(snapshot.ToItem(
                skipReason == null ? ExternalMemberMigrationOutcome.WouldMigrate : ExternalMemberMigrationOutcome.Skipped,
                skipReason));
        }

        return new ExternalMemberMigrationReport(DryRun: true, items);
    }

    /// <summary>
    /// Converts up to <paramref name="limit"/> eligible members (all of them when null). Members that
    /// fail a check are skipped and reported, never deleted.
    /// </summary>
    /// <exception cref="ExternalMemberMigrationAlreadyRunningException">Another run is already in progress, on this or another instance.</exception>
    public async Task<ExternalMemberMigrationReport> MigrateAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        if (!await RunLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            throw new ExternalMemberMigrationAlreadyRunningException("An external member migration is already running.");
        }

        string? leaseClaim = null;
        try
        {
            leaseClaim = _lease.TryAcquire()
                ?? throw new ExternalMemberMigrationAlreadyRunningException("An external member migration is already running on another instance.");

            var items = new List<ExternalMemberMigrationItem>();
            var migrated = 0;
            foreach (var member in GetContentMembers())
            {
                if (cancellationToken.IsCancellationRequested || (limit.HasValue && migrated >= limit.Value))
                {
                    break;
                }

                var snapshot = Snapshot(member);
                var skipReason = await GetSkipReasonAsync(member, snapshot);
                if (skipReason != null)
                {
                    items.Add(snapshot.ToItem(ExternalMemberMigrationOutcome.Skipped, skipReason));
                    continue;
                }

                var item = await MigrateMemberAsync(member, snapshot);
                items.Add(item);
                if (item.Outcome == ExternalMemberMigrationOutcome.Migrated)
                {
                    migrated++;
                }
            }

            _logger.LogInformation(
                "External member migration finished: {Migrated} migrated, {Skipped} skipped, {Failed} failed.",
                items.Count(i => i.Outcome == ExternalMemberMigrationOutcome.Migrated),
                items.Count(i => i.Outcome == ExternalMemberMigrationOutcome.Skipped),
                items.Count(i => i.Outcome == ExternalMemberMigrationOutcome.Failed));

            return new ExternalMemberMigrationReport(DryRun: false, items);
        }
        finally
        {
            if (leaseClaim != null)
            {
                try
                {
                    _lease.Release(leaseClaim);
                }
                catch (Exception ex)
                {
                    // Not fatal: the claim expires on its own.
                    _logger.LogWarning(ex, "Could not release the external member migration lease; it expires after {Duration}.", ExternalMemberMigrationLease.Duration);
                }
            }

            RunLock.Release();
        }
    }

    private IEnumerable<IMember> GetContentMembers() =>
        _memberService.GetMembersByMemberType(CommunityMember.ModelTypeAlias).OrderBy(m => m.Id).ToList();

    private MemberSnapshot Snapshot(IMember member) => new(
        member.Key,
        member.Username,
        member.Email,
        member.Name,
        member.IsApproved,
        member.IsLockedOut,
        member.LastLoginDate,
        member.LastLockoutDate,
        member.CreateDate,
        member.SecurityStamp,
        _memberService.GetAllRoles(member.Id).ToArray(),
        _externalLoginService.GetExternalLogins(member.Key).ToArray(),
        _externalLoginService.GetExternalLoginTokens(member.Key).ToArray());

    private async Task<string?> GetSkipReasonAsync(IMember member, MemberSnapshot snapshot)
    {
        if (!snapshot.Logins.Any(l => l.LoginProvider == GitHubExternalLoginProviderOptions.SchemeName))
        {
            return "No GitHub login is linked, so the member could not sign in as an external member.";
        }

        if (await _externalMemberService.GetByKeyAsync(member.Key) != null)
        {
            return "An external member with this key already exists; resolve the duplicate by hand.";
        }

        if (!string.IsNullOrEmpty(member.Comments))
        {
            return "The member has backoffice comments, which external members can't store.";
        }

        var propertiesWithValues = member.Properties
            .Where(p => p.Values.Any(v => v.EditedValue != null || v.PublishedValue != null))
            .Select(p => p.Alias)
            .ToArray();
        if (propertiesWithValues.Length > 0)
        {
            return $"The member has content property values that would be lost: {string.Join(", ", propertiesWithValues)}.";
        }

        return null;
    }

    private async Task<ExternalMemberMigrationItem> MigrateMemberAsync(IMember member, MemberSnapshot snapshot)
    {
        // Step 1+2: swap the content member for an external member with the same key, atomically.
        using (var scope = _scopeProvider.CreateCoreScope())
        {
            _memberService.Delete(member);

            var created = await _externalMemberService.CreateAsync(new ExternalMemberIdentity
            {
                Key = snapshot.Key,
                Email = snapshot.Email,
                UserName = snapshot.UserName,
                Name = snapshot.Name,
                IsApproved = snapshot.IsApproved,
                IsLockedOut = snapshot.IsLockedOut,
                LastLoginDate = snapshot.LastLoginDate,
                LastLockoutDate = snapshot.LastLockoutDate,
                CreateDate = snapshot.CreateDate,
                SecurityStamp = snapshot.SecurityStamp,
            });
            if (!created.Success)
            {
                // Not completing the scope rolls the delete back.
                return snapshot.ToItem(ExternalMemberMigrationOutcome.Failed, $"Creating the external member failed: {created.Status}.");
            }

            if (snapshot.Groups.Length > 0)
            {
                var assigned = await _externalMemberService.AssignRolesAsync(snapshot.Key, snapshot.Groups);
                if (!assigned.Success)
                {
                    return snapshot.ToItem(ExternalMemberMigrationOutcome.Failed, $"Assigning member groups failed: {assigned.Status}.");
                }
            }

            scope.Complete();
        }

        // Step 3: after the commit (and the post-commit login clean-up), re-link the logins.
        try
        {
            _externalLoginService.Save(snapshot.Key, snapshot.Logins.Select(l => (IExternalLogin)new ExternalLogin(l.LoginProvider, l.ProviderKey, l.UserData)).ToArray());
            if (snapshot.Tokens.Length > 0)
            {
                _externalLoginService.Save(snapshot.Key, snapshot.Tokens.Select(t => (IExternalLoginToken)new ExternalLoginToken(t.LoginProvider, t.Name, t.Value)).ToArray());
            }

            var relinked = _externalLoginService.GetExternalLogins(snapshot.Key)
                .Any(l => l.LoginProvider == GitHubExternalLoginProviderOptions.SchemeName);
            if (!relinked)
            {
                throw new InvalidOperationException("The GitHub login was not found after saving it.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Member {MemberKey} ({UserName}) was converted to an external member but its logins could not be re-linked. " +
                "It re-links on the member's next sign-in if their GitHub email is unchanged. Logins to restore: {Logins}",
                snapshot.Key,
                snapshot.UserName,
                snapshot.Logins.Select(l => $"{l.LoginProvider}:{l.ProviderKey}"));
            return snapshot.ToItem(
                ExternalMemberMigrationOutcome.Failed,
                "Converted, but re-linking the GitHub login failed (see the error log). It re-links on the member's next sign-in if their GitHub email is unchanged.");
        }

        _logger.LogInformation("Converted member {MemberKey} ({UserName}) to an external member.", snapshot.Key, snapshot.UserName);
        return snapshot.ToItem(ExternalMemberMigrationOutcome.Migrated);
    }

    private sealed record MemberSnapshot(
        Guid Key,
        string UserName,
        string Email,
        string? Name,
        bool IsApproved,
        bool IsLockedOut,
        DateTime? LastLoginDate,
        DateTime? LastLockoutDate,
        DateTime CreateDate,
        string? SecurityStamp,
        string[] Groups,
        IIdentityUserLogin[] Logins,
        IIdentityUserToken[] Tokens)
    {
        public ExternalMemberMigrationItem ToItem(ExternalMemberMigrationOutcome outcome, string? reason = null) => new(
            Key,
            UserName,
            Groups,
            Logins.Select(l => l.LoginProvider).Distinct().ToArray(),
            outcome,
            reason);
    }
}
