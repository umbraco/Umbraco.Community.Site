using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace UmbracoCommunity.SearchTracker.Reporting;

public sealed class SiteScope
{
    public static readonly SiteScope None = new(new HashSet<Guid>(), hasFullAccess: false);
    public static readonly SiteScope Full = new(new HashSet<Guid>(), hasFullAccess: true);

    public SiteScope(HashSet<Guid> siteKeys, bool hasFullAccess)
    {
        SiteKeys = siteKeys;
        HasFullAccess = hasFullAccess;
    }

    public HashSet<Guid> SiteKeys { get; }
    public bool HasFullAccess { get; }

    public bool CanAccess(Guid siteKey) => HasFullAccess || SiteKeys.Contains(siteKey);
}

public interface ISiteScopeService
{
    SiteScope GetCurrentScope();
}

/// <summary>A backoffice user sees searches for the sites (content roots) their start nodes sit under.</summary>
internal sealed class SiteScopeService : ISiteScopeService
{
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IEntityService _entityService;
    private readonly AppCaches _appCaches;

    public SiteScopeService(IBackOfficeSecurityAccessor security, IEntityService entityService, AppCaches appCaches)
    {
        _security = security;
        _entityService = entityService;
        _appCaches = appCaches;
    }

    public SiteScope GetCurrentScope()
    {
        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null) return SiteScope.None;

        var startIds = user.CalculateContentStartNodeIds(_entityService, _appCaches);
        if (startIds is null || startIds.Length == 0 || startIds.Contains(Umbraco.Cms.Core.Constants.System.Root))
        {
            return SiteScope.Full;
        }

        var rootIds = _entityService.GetAll(UmbracoObjectTypes.Document, startIds)
            .Select(e => e.Path.Split(',', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault())
            .Select(id => int.TryParse(id, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct();

        var keys = new HashSet<Guid>();
        foreach (var rootId in rootIds)
        {
            var key = _entityService.GetKey(rootId, UmbracoObjectTypes.Document);
            if (key.Success) keys.Add(key.Result);
        }

        return new SiteScope(keys, hasFullAccess: false);
    }
}
