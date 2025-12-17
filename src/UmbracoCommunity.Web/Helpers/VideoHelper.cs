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

            if (videoUrl.Contains("youtube.com/watch?v="))
            {
                videoUrl = videoUrl.Replace("watch?v=", "embed/");
            }
            else if (videoUrl.Contains("youtu.be/"))
            {
                videoUrl = videoUrl.Replace("youtu.be/", "www.youtube.com/embed/");
            }
            var parsedUrl = new UriBuilder(videoUrl);

            var queryString = HttpUtility.ParseQueryString(parsedUrl.Query);
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

            queryString["enablejsapi"] = "1";

            parsedUrl.Query = queryString.ToString() ?? string.Empty;

            return parsedUrl.ToString();
        }
    }
}
