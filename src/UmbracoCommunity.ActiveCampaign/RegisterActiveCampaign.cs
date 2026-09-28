using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Forms.Core.Providers.Extensions;
using UmbracoCommunity.ActiveCampaign.Workflows;

namespace UmbracoCommunity.ActiveCampaign;

/// <summary>
/// Wires up the <see cref="SubscribeToActiveCampaignWorkflow"/> Forms workflow type: options, its named
/// <see cref="HttpClient"/>, and a startup warning when the section isn't filled in. Self-registering like
/// <c>RegisterMeetBooking</c> — a project reference is enough; nothing is sent until an editor attaches the workflow
/// to a form.
/// </summary>
public sealed class RegisterActiveCampaign : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<ActiveCampaignOptions>()
            .Bind(builder.Config.GetSection(ActiveCampaignOptions.SectionName));

        // Base address and Api-Token are applied per request from IOptionsMonitor rather than baked in here, so a
        // config change is picked up and a missing key fails the workflow instead of the client construction.
        builder.Services.AddHttpClient(SubscribeToActiveCampaignWorkflow.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.FormsWorkflows().Add<SubscribeToActiveCampaignWorkflow>();

        // Loud but non-fatal: a missing key should show up in the log at startup, not as failed entries weeks later.
        builder.Services.AddSingleton<IStartupFilter>(sp => new ConfigurationWarningStartupFilter(
            sp.GetRequiredService<IOptionsMonitor<ActiveCampaignOptions>>(),
            sp.GetRequiredService<ILogger<RegisterActiveCampaign>>()));
    }

    private sealed class ConfigurationWarningStartupFilter(IOptionsMonitor<ActiveCampaignOptions> options, ILogger logger)
        : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            if (!options.CurrentValue.IsConfigured)
            {
                logger.LogWarning("ActiveCampaign:ApiKey is not set (or ActiveCampaign:BaseUrl is missing / not an absolute http(s) URL); the Subscribe to ActiveCampaign workflow will fail until it is (appsettings.Local.json locally, environment variable / Cloud portal secret when deployed).");
            }

            return next;
        }
    }
}
