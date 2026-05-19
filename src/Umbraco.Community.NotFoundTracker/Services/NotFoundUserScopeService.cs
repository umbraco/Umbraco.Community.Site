using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.NotFoundTracker.Matching;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundUserScopeService : INotFoundUserScopeService
{
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IDomainService _domainService;
    private readonly ILogger<NotFoundUserScopeService> _logger;

    public NotFoundUserScopeService(
        IBackOfficeSecurityAccessor security,
        IDomainService domainService,
        ILogger<NotFoundUserScopeService> logger)
    {
        _security = security;
        _domainService = domainService;
        _logger = logger;
    }

    public async Task<UserScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return new UserScope(new HashSet<string>(StringComparer.Ordinal), hasFullAccess: false);
        }

        var startNodes = user.StartContentIds ?? Array.Empty<int>();

        // Full-access if user has no start-node restriction (Umbraco's default for admins) or the
        // explicit root marker (-1). Either signals "unrestricted content access".
        if (startNodes.Length == 0 || startNodes.Contains(-1))
        {
            return new UserScope(new HashSet<string>(StringComparer.Ordinal), hasFullAccess: true);
        }

        var accessible = new HashSet<string>(StringComparer.Ordinal);

        // Map: each domain row has a RootContentId — collect domains whose root content
        // is in the user's start node set. (Descendant-node domain lookups can be added
        // later if needed; for v1 we only honour domains directly assigned to start nodes.)
        var startNodeSet = new HashSet<int>(startNodes);
        var domains = await _domainService.GetAllAsync(includeWildcards: true).ConfigureAwait(false);
        foreach (var domain in domains)
        {
            if (domain.RootContentId is null) continue;
            if (!startNodeSet.Contains(domain.RootContentId.Value)) continue;
            if (string.IsNullOrEmpty(domain.DomainName)) continue;

            accessible.Add(UrlNormalizer.NormalizeHostname(domain.DomainName));
        }

        return new UserScope(accessible, hasFullAccess: false);
    }
}
