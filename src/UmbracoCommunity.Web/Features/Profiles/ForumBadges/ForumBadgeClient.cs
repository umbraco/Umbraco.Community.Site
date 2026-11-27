using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Profiles.ForumBadges;

/// <summary>
/// Reads a forum user's badges from the Umbraco forum's public, anonymous Discourse endpoint
/// (<c>https://forum.umbraco.com/user-badges/{username}.json</c>), the same data the forum's own badges page shows.
/// Only a username that passes <see cref="ForumProfileUrl.IsValidUsername"/> is ever asked for, and the host is fixed by
/// the <see cref="HttpClient"/> configured in <see cref="RegisterMemberProfiles"/>.
/// Best effort: any failure is logged and returned as <see cref="ForumBadgesResult.Unavailable"/>, never thrown.
/// Answers are kept in memory per username for <see cref="CacheDuration"/> (an unknown user too), failures for
/// <see cref="FailureCacheDuration"/>, so a slow or broken forum is not asked again on every page view.
/// </summary>
public partial class ForumBadgeClient
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    public static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>No member needs more than this many badges listed; it also bounds what a strange answer can cost.</summary>
    public const int MaxBadges = 200;

    public const int MaxDescriptionLength = 300;

    private static readonly Uri ForumBase = new($"https://{ForumProfileUrl.Host}/");

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ForumBadgeClient> _logger;

    public ForumBadgeClient(HttpClient httpClient, IMemoryCache cache, ILogger<ForumBadgeClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    public async Task<ForumBadgesResult> GetBadgesAsync(string? username, CancellationToken cancellationToken = default)
    {
        if (!ForumProfileUrl.IsValidUsername(username))
        {
            return ForumBadgesResult.NotFound;
        }

        var cacheKey = $"forum-badges:{username!.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ForumBadgesResult? cached) && cached != null)
        {
            return cached;
        }

        var result = await FetchAsync(username, cancellationToken);
        _cache.Set(cacheKey, result, result.Status == ForumBadgesStatus.Unavailable ? FailureCacheDuration : CacheDuration);
        return result;
    }

    private async Task<ForumBadgesResult> FetchAsync(string username, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"user-badges/{Uri.EscapeDataString(username)}.json", cancellationToken);

            // An unknown user is an answer, not a failure.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ForumBadgesResult.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Forum badge lookup for {Username} returned {StatusCode}", username, (int)response.StatusCode);
                return ForumBadgesResult.Unavailable;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Forum badge lookup for {Username} returned something other than a JSON object", username);
                return ForumBadgesResult.Unavailable;
            }

            return ForumBadgesResult.Ok(Parse(document.RootElement));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Forum badge lookup for {Username} failed", username);
            return ForumBadgesResult.Unavailable;
        }
    }

    /// <summary>
    /// Reads the user's badges from a <c>user-badges/{username}.json</c> answer: one per badge the user was granted
    /// (a badge granted more than once is listed once, with its first grant), Gold first, then Silver, then Bronze, then by
    /// name. Anything missing or of the wrong shape is skipped rather than failing the whole list.
    /// </summary>
    public static IReadOnlyList<ForumBadge> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        // Badge types: id → (name, sort order). Discourse ranks Gold highest.
        var types = new Dictionary<int, (string Name, int Order)>();
        foreach (var type in Array(root, "badge_types"))
        {
            if (ReadInt(type, "id") is { } id && ReadString(type, "name") is { } name)
            {
                types[id] = (name, ReadInt(type, "sort_order") ?? 0);
            }
        }

        var definitions = new Dictionary<int, JsonElement>();
        foreach (var badge in Array(root, "badges"))
        {
            if (ReadInt(badge, "id") is { } id)
            {
                definitions.TryAdd(id, badge);
            }
        }

        // The first grant of each badge the user holds.
        var granted = new Dictionary<int, DateTimeOffset?>();
        foreach (var userBadge in Array(root, "user_badges"))
        {
            if (ReadInt(userBadge, "badge_id") is not { } badgeId)
            {
                continue;
            }

            var at = ReadDate(userBadge, "granted_at");
            if (!granted.TryGetValue(badgeId, out var earlier))
            {
                granted[badgeId] = at;
            }
            else if (at != null && (earlier == null || at < earlier))
            {
                granted[badgeId] = at;
            }
        }

        var badges = new List<(ForumBadge Badge, int Order)>();
        foreach (var (badgeId, grantedAt) in granted)
        {
            if (!definitions.TryGetValue(badgeId, out var definition)
                || ReadBool(definition, "enabled") == false
                || ReadString(definition, "name") is not { } name
                || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var typeId = ReadInt(definition, "badge_type_id");
            var type = typeId is { } t && types.TryGetValue(t, out var known) ? known : ((string Name, int Order)?)null;

            badges.Add((new ForumBadge(
                badgeId,
                name.Trim(),
                PlainText(ReadString(definition, "description")),
                ImageUrl(ReadString(definition, "image_url")),
                type?.Name,
                ReadInt(definition, "grant_count"),
                grantedAt,
                ForumProfileUrl.BadgeSlug(ReadString(definition, "slug"), name)), type?.Order ?? int.MinValue));
        }

        return badges
            .OrderByDescending(b => b.Order)
            .ThenBy(b => b.Badge.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Badge.Id)
            .Take(MaxBadges)
            .Select(b => b.Badge)
            .ToList();
    }

    /// <summary>The forum's HTML description as plain text, cut to <see cref="MaxDescriptionLength"/>; null when empty.</summary>
    public static string? PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = Whitespace().Replace(WebUtility.HtmlDecode(HtmlTag().Replace(html, " ")), " ").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= MaxDescriptionLength ? text : text[..(MaxDescriptionLength - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// A badge picture's absolute https address. The forum gives its own uploads as a path ("/uploads/..."), which is
    /// resolved against the forum; any other value that is not an https address is ignored.
    /// </summary>
    public static string? ImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = "https:" + value;
        }
        else if (value.StartsWith('/'))
        {
            return Uri.TryCreate(ForumBase, value, out var onForum) ? onForum.AbsoluteUri : null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.AbsoluteUri
            : null;
    }

    private static IEnumerable<JsonElement> Array(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
            : [];

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool? ReadBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement element, string name) =>
        ReadString(element, name) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;
}
