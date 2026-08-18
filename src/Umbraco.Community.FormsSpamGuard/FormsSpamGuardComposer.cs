using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.FormsSpamGuard.Configuration;
using Umbraco.Community.FormsSpamGuard.FieldTypes;
using Umbraco.Community.FormsSpamGuard.Signals;
using Umbraco.Community.FormsSpamGuard.Tokens;
using Umbraco.Forms.Core.Providers.Extensions;

namespace Umbraco.Community.FormsSpamGuard;

/// <summary>
/// Registers the spam guard field type and everything it depends on.
/// </summary>
/// <remarks>
/// Unlike <c>Umbraco.Community.NotFoundTracker</c>, this composer registers itself rather than requiring an
/// explicit opt-in call from the host: the package has no abstract member the host must supply, so adding the
/// project reference is enough. The field still does nothing until an editor places it on a form.
/// </remarks>
public class FormsSpamGuardComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<FormsSpamGuardOptions>()
            .Bind(builder.Config.GetSection(Constants.ConfigurationSection));

        // TimeProvider.System is the framework default, but registering it explicitly means tests can swap in a
        // FakeTimeProvider without the package depending on how the host wires things up.
        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.AddSingleton<ISpamGuardTokenService, SpamGuardTokenService>();

        builder.Services.AddSingleton<ISpamSignal, DecoyFieldSignal>();
        builder.Services.AddSingleton<ISpamSignal, SubmissionTimingSignal>();
        builder.Services.AddSingleton<ISpamSignal, JavaScriptTokenSignal>();

        builder.FormsFields().Add<SpamGuardField>();
    }
}
