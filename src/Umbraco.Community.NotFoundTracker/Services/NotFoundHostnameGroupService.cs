using Umbraco.Cms.Core.Services;
using Umbraco.Community.NotFoundTracker.Matching;

namespace Umbraco.Community.NotFoundTracker.Services;

/// <summary>
/// Builds the grouped hostname listing for the hits filter dropdown. Groups hostnames by the
/// root content node they're assigned to in Umbraco's domain table; any hostnames seen in the
/// hits table but not configured on a node land in an "Other" group.
/// </summary>
public sealed class NotFoundHostnameGroupService
{
    private readonly IDomainService _domainService;
    private readonly IEntityService _entityService;
    private readonly INotFoundHitService _hitService;

    public NotFoundHostnameGroupService(
        IDomainService domainService,
        IEntityService entityService,
        INotFoundHitService hitService)
    {
        _domainService = domainService;
        _entityService = entityService;
        _hitService = hitService;
    }

    public async Task<IReadOnlyList<HostnameGroup>> GetGroupsAsync(UserScope scope, CancellationToken ct)
    {
        var seenHostnames = await _hitService.GetDistinctHostnamesAsync(scope, ct);
        var seenSet = new HashSet<string>(seenHostnames, StringComparer.Ordinal);

        var domains = await _domainService.GetAllAsync(includeWildcards: true).ConfigureAwait(false);

        // node id -> set of normalized hostnames
        var byNode = new Dictionary<int, HashSet<string>>();
        foreach (var domain in domains)
        {
            if (domain.RootContentId is null) continue;
            if (string.IsNullOrEmpty(domain.DomainName)) continue;

            var host = UrlNormalizer.NormalizeHostname(domain.DomainName);
            if (string.IsNullOrEmpty(host)) continue;

            if (!byNode.TryGetValue(domain.RootContentId.Value, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                byNode[domain.RootContentId.Value] = set;
            }
            set.Add(host);
        }

        var groups = new List<HostnameGroup>();
        var mapped = new HashSet<string>(StringComparer.Ordinal);

        if (byNode.Count > 0)
        {
            var entities = _entityService.GetAll(Umbraco.Cms.Core.Models.UmbracoObjectTypes.Document, byNode.Keys.ToArray())
                .ToDictionary(e => e.Id);

            foreach (var (nodeId, hostnames) in byNode.OrderBy(kv => entities.TryGetValue(kv.Key, out var ent) ? ent.Name : string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (!entities.TryGetValue(nodeId, out var entity)) continue;

                // Filter the group to hostnames the user can access. If none, skip the group.
                var accessible = hostnames.Where(scope.CanAccessHostname).ToList();
                if (accessible.Count == 0) continue;

                foreach (var h in accessible) mapped.Add(h);

                groups.Add(new HostnameGroup
                {
                    NodeKey = entity.Key,
                    NodeName = entity.Name ?? string.Empty,
                    Hostnames = accessible.OrderBy(h => h, StringComparer.Ordinal).ToList(),
                });
            }
        }

        var otherHostnames = seenSet.Except(mapped).OrderBy(h => h, StringComparer.Ordinal).ToList();
        if (otherHostnames.Count > 0)
        {
            groups.Add(new HostnameGroup
            {
                NodeKey = null,
                NodeName = "Other",
                Hostnames = otherHostnames,
            });
        }

        return groups;
    }
}

public sealed class HostnameGroup
{
    public Guid? NodeKey { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public List<string> Hostnames { get; set; } = new();
}
