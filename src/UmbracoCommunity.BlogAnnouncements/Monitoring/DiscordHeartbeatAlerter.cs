using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.BlogAnnouncements.Monitoring;

/// <summary>
/// Posts stale-pipeline alerts to a Discord webhook. Deliberately separate from
/// <c>DiscordWebhookAnnouncer</c>: this one must point at an operations channel, and it must keep
/// working when the announcement path does not — that is the whole point of it.
/// </summary>
public sealed class DiscordHeartbeatAlerter : IHeartbeatAlerter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Discord embed colour (red), so the alert reads as a problem at a glance.</summary>
    private const int AlertColour = 0xE5484D;

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<BlogAnnouncementsOptions> _options;
    private readonly ILogger<DiscordHeartbeatAlerter> _logger;

    public DiscordHeartbeatAlerter(
        HeartbeatAlerterHttpClient typedClient,
        IOptionsMonitor<BlogAnnouncementsOptions> options,
        ILogger<DiscordHeartbeatAlerter> logger)
    {
        _http = typedClient.Client;
        _options = options;
        _logger = logger;
    }

    public static object BuildBody(StaleRunAlert alert) => new
    {
        username = "Blog Announcements Monitor",
        embeds = new[]
        {
            new
            {
                title = "Blog announcements have stopped",
                description =
                    $"No announcement cycle has completed for {FormatAge(alert.Age)}. " +
                    // Invariant: ':' is a culture-dependent time separator, so a server running a
                    // non-invariant culture would otherwise render this as "08.46".
                    $"The last one ran at {alert.LastRunUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC.\n\n" +
                    "The pipeline runs every 15 minutes. If Umbraco Automate's engine failed to " +
                    "start, scheduled triggers stay paused until the site is restarted.",
                color = AlertColour,
            },
        },
    };

    /// <summary>
    /// Plain words rather than a duration literal — the alert is read by a person deciding whether
    /// to get up, so "2 days" has to be unmissable where "2.00:00:00" is not.
    /// </summary>
    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
        {
            var days = (int)age.TotalDays;
            var hours = age.Hours;
            return hours > 0
                ? $"{days} {Plural(days, "day")} {hours} {Plural(hours, "hour")}"
                : $"{days} {Plural(days, "day")}";
        }

        if (age.TotalHours >= 1)
        {
            var hours = (int)age.TotalHours;
            var minutes = age.Minutes;
            return minutes > 0
                ? $"{hours} {Plural(hours, "hour")} {minutes} {Plural(minutes, "minute")}"
                : $"{hours} {Plural(hours, "hour")}";
        }

        var wholeMinutes = Math.Max(1, (int)age.TotalMinutes);
        return $"{wholeMinutes} {Plural(wholeMinutes, "minute")}";
    }

    private static string Plural(int value, string unit) => value == 1 ? unit : unit + "s";

    public async Task<bool> AlertAsync(StaleRunAlert alert, CancellationToken cancellationToken)
    {
        var webhookUrl = _options.CurrentValue.Monitoring.Discord.WebhookUrl;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning(
                "BlogAnnouncements pipeline is stale ({Age}) but no monitoring webhook is configured, so no alert was sent.",
                alert.Age);
            return false;
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(
                webhookUrl, BuildBody(alert), JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Monitoring webhook returned {Status} sending a stale-pipeline alert. Response: {Response}",
                (int)response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send the stale-pipeline alert.");
            return false;
        }
    }
}

/// <summary>Typed HttpClient wrapper for posting to the operations webhook.</summary>
public sealed class HeartbeatAlerterHttpClient
{
    public HeartbeatAlerterHttpClient(HttpClient client) => Client = client;

    public HttpClient Client { get; }
}
