using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundRedirectService : INotFoundRedirectService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly IRedirectUrlService _redirectUrlService;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly ILogger<NotFoundRedirectService> _logger;

    public NotFoundRedirectService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        IRedirectUrlService redirectUrlService,
        IUmbracoContextAccessor umbracoContextAccessor,
        ILogger<NotFoundRedirectService> logger)
    {
        _contextFactory = contextFactory;
        _redirectUrlService = redirectUrlService;
        _umbracoContextAccessor = umbracoContextAccessor;
        _logger = logger;
    }

    public async Task<RedirectResult> CreateRedirectForHitAsync(
        int hitId, Guid targetContentKey, string? culture, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var hit = await context.NotFoundHits
            .Include(h => h.QueryStrings)
            .FirstOrDefaultAsync(h => h.Id == hitId, ct);
        if (hit is null) return new RedirectResult(RedirectResultKind.HitNotFound);

        if (!scope.CanAccessHostname(hit.Hostname))
        {
            return new RedirectResult(RedirectResultKind.Forbidden,
                Reason: $"User cannot access hostname '{hit.Hostname}'.");
        }

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
        {
            return new RedirectResult(RedirectResultKind.Failed, Reason: "UmbracoContext unavailable.");
        }

        var targetContent = umbracoContext.Content?.GetById(targetContentKey);
        if (targetContent is null)
        {
            return new RedirectResult(RedirectResultKind.TargetContentNotFound);
        }

        // The full URL the editor wants to register is (hostname + path). Umbraco's
        // RedirectUrlService.Register stores by URL and resolves on incoming request match.
        var url = string.IsNullOrEmpty(hit.Hostname)
            ? hit.Path
            : $"{hit.Hostname}{hit.Path}";

        try
        {
            _redirectUrlService.Register(url, targetContent.Key, culture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register redirect for hit {HitId} (url='{Url}')", hitId, url);
            return new RedirectResult(RedirectResultKind.Failed, Reason: ex.Message);
        }

        hit.Status = HitStatus.Redirected;
        context.NotFoundHitQueryStrings.RemoveRange(hit.QueryStrings);
        await context.SaveChangesAsync(ct);

        return new RedirectResult(RedirectResultKind.Ok);
    }
}
