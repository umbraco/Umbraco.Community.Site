namespace UmbracoCommunity.Web
{
    public static partial class Constants
    {
        public static class Security
        {
            public const string AllowedImageTypes = "gif,jpg,jpeg,png,tiff,webp";

            public static readonly string[] DefaultAllowDomains = [];

            public static readonly string[] DefaultAllowStyles = [];

            public static readonly string[] DefaultAllowFonts = ["fonts.gstatic.com", "data:"];

            public static readonly string[] DefaultAllowWorkers = [];

            public static readonly string[] DefaultAllowConnections = [];

            public static readonly string[] DefaultAllowFrames = ["*.youtube.com", "*.walls.io"];

            public static readonly string[] DefaultAllowScripts = ["*.youtube.com"];

            public static readonly string[] DefaultAllowImages = ["avatars.githubusercontent.com", "github.com", "api.dicebear.com", "images.pexels.com", "data:", "sessionize.com", "cache.sessionize.com", "assets.sessionize.com", "cdn.sessionize.com"];

            public static readonly string[] DefaultAllowFormActions = ["github.com"];

            public static readonly string[] DefaultAllowMedia = [];
        }
    }
}
