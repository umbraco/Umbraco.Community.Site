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

    public UserScope GetCurrentScope()
    {
        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return new UserScope(new HashSet<string>(StringComparer.Ordinal), hasFullAccess: false);
        }

        var startNodes = user.StartContentIds ?? Array.Empty<int>();

        // Full-access if user has root (-1) as a start node.
        if (startNodes.Contains(-1))
        {
            return new UserScope(new HashSet<string>(StringComparer.Ordinal), hasFullAccess: true);
        }

        var accessible = new HashSet<string>(StringComparer.Ordinal);
        if (startNodes.Length == 0)
        {
            return new UserScope(accessible, hasFullAccess: false);
        }

        // Map: each domain row has a RootContentId — collect domains whose root content
        // is in the user's start node set. (Descendant-node domain lookups can be added
        // later if needed; for v1 we only honour domains directly assigned to start nodes.)
        var startNodeSet = new HashSet<int>(startNodes);
        foreach (var domain in _domainService.GetAll(includeWildcards: true))
        {
            if (domain.RootContentId is null) continue;
            if (!startNodeSet.Contains(domain.RootContentId.Value)) continue;
            if (string.IsNullOrEmpty(domain.DomainName)) continue;

            accessible.Add(UrlNormalizer.NormalizeHostname(domain.DomainName));
        }

        return new UserScope(accessible, hasFullAccess: false);
    }
}
