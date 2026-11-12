using System.Text.RegularExpressions;
using System.Web;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Helpers
{
    public static class VideoHelper
    {
        public static string GetAllow(this SettingsVideoBlock? videoSettings)
        {
            var allows = new List<string> {
                "accelerometer",
                "clipboard-write",
                "encrypted-media",
                "fullscreen",
                "gyroscope",
                "picture-in-picture",
                "autoplay"
            };

            return string.Join("; ", allows);
        }

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
            var start = sourceQuery["t"] ?? sourceQuery["start"];
            if (int.TryParse(start?.TrimEnd('s'), out var startSeconds) && startSeconds > 0)
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

        private static readonly Regex YouTubeIdRegex = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
    }
}
