namespace Umbraco.Community.NotFoundTracker.Services;

/// <summary>
/// Per-request multi-tenant scope for backoffice users. Derives the set of hostnames
/// the current user is allowed to see + mutate based on their Umbraco start nodes and
/// the domains assigned to those nodes (or their descendants).
/// </summary>
public interface INotFoundUserScopeService
{
    Task<UserScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default);
}

public sealed class UserScope
{
    public HashSet<string> AccessibleHostnames { get; }
    public bool HasFullAccess { get; }

    public UserScope(HashSet<string> accessibleHostnames, bool hasFullAccess)
    {
        AccessibleHostnames = accessibleHostnames;
        HasFullAccess = hasFullAccess;
    }

    public bool CanAccessHostname(string hostname)
        => HasFullAccess || AccessibleHostnames.Contains(hostname);
}
