using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Static, code-shipped list of common scanner paths to ignore by default.
/// Inserted on first boot with <see cref="IgnoreRuleSource.AutoPreset"/>.
/// Editor deletions persist (never re-seeded for an already-deleted entry).
///
/// Deliberately excludes things editors should see (sitemap.xml, robots.txt,
/// favicon.ico, .well-known/) so a missing-but-expected URL surfaces as a 404
/// in the dashboard rather than being silently dropped.
/// </summary>
public static class DefaultIgnoreRules
{
    public static readonly IReadOnlyList<DefaultIgnoreRule> All = new[]
    {
        // WordPress / PHP CMS probes
        Prefix("/wp-admin"),
        Prefix("/wp-login"),
        Prefix("/wp-content"),
        Prefix("/wp-includes"),
        Prefix("/wp-json"),
        Exact("/xmlrpc.php"),
        Exact("/wlwmanifest.xml"),

        // Config / secret leak probes
        Exact("/.env"),
        Prefix("/.git"),
        Prefix("/.svn"),
        Prefix("/.aws"),

        // PHP / classic admin paths
        Prefix("/phpmyadmin"),
        Prefix("/pma"),
        Prefix("/myadmin"),
        Exact("/adminer.php"),
        Exact("/admin.php"),

        // IIS / .NET legacy probes
        Prefix("/owa"),
        Prefix("/ecp"),
        Prefix("/autodiscover"),
        Exact("/telerik.web.ui.webresource.axd"),
        Exact("/elmah.axd"),
        Exact("/trace.axd"),

        // Other CMS / framework scanners
        Prefix("/drupal"),
        Prefix("/joomla"),
        Prefix("/typo3"),
        Prefix("/magento"),
        Prefix("/bitrix"),
        Prefix("/laravel"),
        Exact("/.htaccess"),
        Exact("/web.config"),

        // Misc nuisance
        Prefix("/cgi-bin"),
        Prefix("/scripts"),
        Prefix("/cgi"),
        Exact("/server-status"),
        Exact("/server-info"),
        Exact("/hnap1"),
        Prefix("/boaform"),
        Exact("/setup.cgi"),
        Exact("/.ds_store"),
        Exact("/thumbs.db"),

        // Noisy bot lookups — keep but editors can delete
        Exact("/ads.txt"),
        Exact("/app-ads.txt"),
        Exact("/security.txt"),
    };

    private static DefaultIgnoreRule Exact(string path) => new(path, IgnoreMatchType.Exact);
    private static DefaultIgnoreRule Prefix(string path) => new(path, IgnoreMatchType.PathPrefix);
}

public sealed record DefaultIgnoreRule(string Path, IgnoreMatchType MatchType);
