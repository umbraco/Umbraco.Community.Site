using System.Text.RegularExpressions;
using System.Web;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Helpers
{
    public static class VideoHelper
    {
        public const string YouTubeAllow = "accelerometer; clipboard-write; encrypted-media; fullscreen; gyroscope; picture-in-picture; autoplay";

        public static string GetAllow(this SettingsVideoBlock? videoSettings) => YouTubeAllow;

        public static string GetYouTubeLink(this string videoUrl, SettingsVideoBlock? settings)
        {

            var videoId = GetYouTubeVideoId(videoUrl);
            if (videoId is null)
            {
                return string.Empty;
            }

            var parsedUrl = new UriBuilder("https", YouTubeEmbedHost) { Path = $"/embed/{videoId}" };

            var queryString = HttpUtility.ParseQueryString(string.Empty);
            var sourceQuery = HttpUtility.ParseQueryString(new Uri(videoUrl).Query);
            if (ParseStartSeconds(sourceQuery["t"] ?? sourceQuery["start"]) is { } startSeconds)
            {
                queryString["start"] = startSeconds.ToString();
            }
            if (settings != null)
            {
                if (settings.Autoplay)
                {
                    queryString["autoplay"] = "1";
                }
                if (settings.Muted)
                {
                    queryString["mute"] = "1";
                }
            }

            parsedUrl.Query = queryString.ToString() ?? string.Empty;

            return parsedUrl.Uri.ToString();
        }

        public const string YouTubeEmbedHost = "www.youtube-nocookie.com";

        public static string? GetYouTubeVideoId(string videoUrl)
        {
            if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var id = host.ToLowerInvariant() switch
            {
                "youtu.be" => segments.FirstOrDefault(),
                "youtube.com" or "m.youtube.com" or "youtube-nocookie.com" => segments.Length >= 2 && segments[0] is "embed" or "shorts" or "live"
                    ? segments[1]
                    : HttpUtility.ParseQueryString(uri.Query)["v"],
                _ => null,
            };

            return id is not null && YouTubeIdRegex.IsMatch(id) ? id : null;
        }

        /// <summary>Seconds from a YouTube start time such as <c>90</c>, <c>90s</c>, <c>1m30s</c> or <c>1h2m3s</c>.</summary>
        internal static int? ParseStartSeconds(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || StartTimeRegex.Match(value.Trim()) is not { Success: true } match)
            {
                return null;
            }

            int Part(string name) => match.Groups[name].Success ? int.Parse(match.Groups[name].Value) : 0;
            var seconds = Part("h") * 3600 + Part("m") * 60 + Part("s");
            return seconds > 0 ? seconds : null;
        }

        private static readonly Regex YouTubeIdRegex = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

        private static readonly Regex StartTimeRegex = new(
            @"^(?:(?<s>\d{1,6})s?|(?:(?<h>\d{1,3})h)?(?:(?<m>\d{1,4})m)?(?:(?<s>\d{1,6})s)?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
