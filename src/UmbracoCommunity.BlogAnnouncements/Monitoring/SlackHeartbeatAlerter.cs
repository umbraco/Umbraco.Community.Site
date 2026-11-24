using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.BlogAnnouncements.Monitoring;

/// <summary>
/// Posts stale-pipeline alerts to a Slack incoming webhook. Separate from the announcement path on
/// purpose: announcements go to the community's Discord, these go to an operations channel, and
/// this one has to keep working when the announcement path does not.
/// </summary>
public sealed class SlackHeartbeatAlerter : IHeartbeatAlerter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<BlogAnnouncementsOptions> _options;
    private readonly ILogger<SlackHeartbeatAlerter> _logger;

    public SlackHeartbeatAlerter(
        HeartbeatAlerterHttpClient typedClient,
        IOptionsMonitor<BlogAnnouncementsOptions> options,
        ILogger<SlackHeartbeatAlerter> logger)
    {
        _http = typedClient.Client;
        _options = options;
        _logger = logger;
    }

    private const string Headline = "Blog announcements have stopped";

    public static object BuildBody(StaleRunAlert alert)
    {
        var age = FormatAge(alert.Age);

        // Invariant: ':' is a culture-dependent time separator, so a server running a
        // non-invariant culture would otherwise render this as "08.46".
        var lastRun = alert.LastRunUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return new
        {
            // Slack shows `text` in push notifications and the channel sidebar, not the blocks —
            // without it the alert arrives as "This content can't be displayed".
            text = $"{Headline} — no cycle for {age}",
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    // A header block only accepts plain_text; mrkdwn is rejected as invalid_blocks.
                    text = new { type = "plain_text", text = Headline },
                },
                new
                {
                    type = "section",
                    text = new
                    {
                        // Slack's mrkdwn: *single asterisks* for bold, `backticks` for code.
                        type = "mrkdwn",
                        text =
                            $"No announcement cycle has completed for *{age}*. " +
                            $"The last one ran at `{lastRun}` UTC.\n\n" +
                            "The pipeline runs every 15 minutes. If Umbraco Automate's engine " +
                            "failed to start, scheduled triggers stay paused until the site is restarted.",
                    },
                },
            },
        };
    }

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
        var webhookUrl = _options.CurrentValue.Monitoring.Slack.WebhookUrl;
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

            // Slack puts the reason in the body (e.g. invalid_payload, no_service), not the status.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Slack webhook returned {Status} sending a stale-pipeline alert: {Response}",
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
