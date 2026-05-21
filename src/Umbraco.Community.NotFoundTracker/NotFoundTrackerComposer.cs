using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.NotFoundTracker;

/// <summary>
/// Package marker composer. Intentionally a no-op: registration must be explicit via
/// <c>builder.AddNotFoundTracker()</c> in the host's own composer, because the host
/// is required to also register an <c>INotFoundPageResolver</c> implementation.
/// </summary>
public class NotFoundTrackerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Intentionally empty. See NotFoundTrackerBuilderExtensions.AddNotFoundTracker().
    }
}
