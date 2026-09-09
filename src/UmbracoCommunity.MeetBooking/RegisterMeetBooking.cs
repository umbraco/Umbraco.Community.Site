using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Forms.Core.Providers.Extensions;
using UmbracoCommunity.MeetBooking.Forms;
using UmbracoCommunity.MeetBooking.Google;
using UmbracoCommunity.MeetBooking.Storage;
using UmbracoCommunity.MeetBooking.Workflows;

namespace UmbracoCommunity.MeetBooking;

/// <summary>
/// Wires up the community Meet booking workflow: options, the Apps Script HTTP client, the record mapper/persister,
/// and the <see cref="CreateMeetWorkflow"/> Forms workflow type. Self-registering like <c>FormsSpamGuard</c> — a
/// project reference is enough; nothing happens until an editor attaches the workflow to a form.
/// </summary>
public sealed class RegisterMeetBooking : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<MeetBookingOptions>()
            .Bind(builder.Config.GetSection(MeetBookingOptions.SectionName));

        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.AddHttpClient<AppsScriptHttpClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<MeetBookingOptions>>().CurrentValue.AppsScript;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(opts.TimeoutSeconds, 10, 300));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        // Provisioning state lives in our own table, not on a hidden Forms field: a Hidden field is stored in
        // UFRecordDataString.Value (nvarchar(255)) and a real state object is ~600 characters. See
        // Storage/MeetProvisioningStateEntity.cs for the full story.
        builder.Services.AddDbContextFactory<MeetBookingDbContext>((sp, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var dataDir = Path.Combine(env.ContentRootPath, "umbraco", "Data");
                options.UseSqlite(connectionString?.Replace("|DataDirectory|", dataDir), sqlite =>
                    sqlite.MigrationsAssembly("UmbracoCommunity.MeetBooking"));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("UmbracoCommunity.MeetBooking");
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                });
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, MeetBookingMigrationNotificationHandler>();

        builder.Services.AddSingleton<IMeetStateStore, EfMeetStateStore>();
        builder.Services.AddSingleton<IRecordPersister, FormsRecordPersister>();
        builder.Services.AddSingleton<IMeetProvisioner, AppsScriptMeetProvisioner>();

        // Slack posts go through our own workflow type rather than Forms' built-in one: that only takes a webhook
        // URL and always dumps every field, which buries the two things the approver needs.
        builder.Services.AddHttpClient(PostMeetToSlackWorkflow.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.FormsWorkflows().Add<CreateMeetWorkflow>();
        builder.FormsWorkflows().Add<PostMeetToSlackWorkflow>();

        // Loud but non-fatal: a missing secret should show up in the log at startup, not as a failed entry weeks later.
        builder.Services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter>(sp => new ConfigurationWarningStartupFilter(
            sp.GetRequiredService<IOptionsMonitor<MeetBookingOptions>>(),
            sp.GetRequiredService<ILogger<RegisterMeetBooking>>()));
    }

    private sealed class ConfigurationWarningStartupFilter(IOptionsMonitor<MeetBookingOptions> options, ILogger logger)
        : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
        {
            var opts = options.CurrentValue;
            if (opts.Enabled && !opts.DryRun &&
                (string.IsNullOrWhiteSpace(opts.AppsScript.WebAppUrl) || string.IsNullOrWhiteSpace(opts.AppsScript.SharedSecret)))
            {
                logger.LogWarning("MeetBooking is enabled but MeetBooking:AppsScript:WebAppUrl / SharedSecret are not set; the Create Meet workflow will fail until they are (appsettings.Local.json or Cloud portal).");
            }

            return next;
        }
    }
}
