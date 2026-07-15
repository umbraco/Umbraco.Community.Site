using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.BlogAnnouncements.Delivery;

/// <summary>
/// Posts an announcement to a Discord incoming webhook as a single rich embed, following the
/// "Discord delivery details" in the pipeline design. Honours the pipeline's dry-run flag: when
/// dry-run is on it logs the would-be payload and posts nothing.
/// </summary>
public sealed class DiscordWebhookAnnouncer : IDiscordAnnouncer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<BlogAnnouncementsOptions> _options;
    private readonly ILogger<DiscordWebhookAnnouncer> _logger;

    public DiscordWebhookAnnouncer(
        DiscordAnnouncerHttpClient typedClient,
        IOptionsMonitor<BlogAnnouncementsOptions> options,
        ILogger<DiscordWebhookAnnouncer> logger)
    {
        _http = typedClient.Client;
        _options = options;
        _logger = logger;
    }

    public async Task<DeliveryResult> AnnounceAsync(AnnouncementPayload payload, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var body = BuildBody(payload, options.PublicBaseUrl);

        if (options.DryRun)
        {
            _logger.LogInformation(
                "[BlogAnnouncements dry-run] Would post to Discord: {Payload}",
                JsonSerializer.Serialize(body, JsonOptions));
            return DeliveryResult.Dry;
        }

        var webhookUrl = options.Discord.WebhookUrl;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning(
                "BlogAnnouncements dry-run is off but no Discord webhook URL is configured; treating '{Title}' as a failed delivery.",
                payload.Title);
            return DeliveryResult.Fail(null);
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(webhookUrl, body, JsonOptions, cancellationToken);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                return DeliveryResult.Ok(status);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Discord webhook returned {Status} announcing '{Title}'. Webhook: {WebhookUrl}. Payload: {Payload}. Response: {Response}",
                status, payload.Title, webhookUrl, JsonSerializer.Serialize(body, JsonOptions), responseBody);
            return DeliveryResult.Fail(status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to post '{Title}' to the Discord webhook. Webhook: {WebhookUrl}. Payload: {Payload}",
                payload.Title, webhookUrl, JsonSerializer.Serialize(body, JsonOptions));
            return DeliveryResult.Fail(null);
        }
    }

    /// <summary>Webhook display name, constant on every message.</summary>
    internal const string WebhookUsername = "Community Blog Posts";

    /// <summary>
    /// Builds the Discord webhook body. The message identity is deliberately constant — the
    /// "Community Blog Posts" username plus the webhook's own configured avatar. Discord groups
    /// consecutive webhook messages sent within its ~7-minute window by username (invisible-char
    /// tricks get sanitized, and a differing avatar doesn't break grouping), so we don't fight it:
    /// with 15-minute polling, real posts announce solo and get individual headers naturally, and
    /// bursts group under one consistent header. The author's identity lives inside each embed's
    /// author row (<c>icon_url</c>), where Discord cannot render SVG avatars — any <c>.svg</c>
    /// avatar URL is dropped so the row degrades to the plain author name.
    /// </summary>
    internal static object BuildBody(AnnouncementPayload payload, string? publicBaseUrl = null)
    {
        var avatar = UsableAvatar(ResolveAbsoluteUrl(payload.AvatarUrl, publicBaseUrl));
        var cover = ResolveAbsoluteUrl(payload.CoverImageUrl, publicBaseUrl);

        var embedAuthor = new Dictionary<string, object?>
        {
            ["name"] = string.IsNullOrWhiteSpace(payload.AuthorName) ? "Umbraco Community" : payload.AuthorName,
        };
        if (avatar is not null)
        {
            embedAuthor["icon_url"] = avatar;
        }
        if (!string.IsNullOrWhiteSpace(payload.AuthorProfileUrl))
        {
            embedAuthor["url"] = payload.AuthorProfileUrl;
        }

        var embed = new Dictionary<string, object?>
        {
            ["author"] = embedAuthor,
            ["title"] = payload.Title,
            ["url"] = payload.Url,
            ["timestamp"] = payload.PublishedAt.ToString("o"),
        };
        if (!string.IsNullOrWhiteSpace(payload.Excerpt))
        {
            embed["description"] = payload.Excerpt;
        }
        if (cover is not null)
        {
            embed["image"] = new Dictionary<string, object?> { ["url"] = cover };
        }

        return new Dictionary<string, object?>
        {
            ["username"] = WebhookUsername,
            ["embeds"] = new[] { embed },
        };
    }

    private static string? UsableAvatar(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return null;
        }

        // Strip a query string before the extension check so "avatar.svg?token=..." is caught too.
        var withoutQuery = avatarUrl.Split('?', 2)[0];
        return withoutQuery.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? null : avatarUrl;
    }

    /// <summary>
    /// Community blog cover images/avatars are localized to root-relative paths (e.g.
    /// <c>/community-blog-images/{file}</c>) by <c>CommunityBlogsImageDownloader</c> so the site's
    /// own pages can load them from 'self'. Discord's embed validator requires a fully-qualified
    /// URL and returns 400 on a relative one, so any URL that isn't already absolute http(s) is
    /// resolved against <paramref name="publicBaseUrl"/> — and dropped (not sent broken) if that
    /// isn't configured or the result still isn't a valid absolute http(s) URL.
    /// </summary>
    private static string? ResolveAbsoluteUrl(string? url, string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (IsHttpUri(url, UriKind.Absolute, out var absolute))
        {
            return absolute!.ToString();
        }

        if (!string.IsNullOrWhiteSpace(publicBaseUrl)
            && IsHttpUri(publicBaseUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, url, out var resolved)
            && IsHttpScheme(resolved))
        {
            return resolved.ToString();
        }

        return null;
    }

    private static bool IsHttpUri(string url, UriKind kind, out Uri? uri)
        => Uri.TryCreate(url, kind, out uri) && IsHttpScheme(uri);

    private static bool IsHttpScheme(Uri? uri)
        => uri is not null && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
