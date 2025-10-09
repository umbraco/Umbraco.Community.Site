namespace UmbracoCommunity.Web
{
    public static partial class Constants
    {
        public static class Security
        {
            public const string AllowedImageTypes = "gif,jpg,jpeg,png,tiff,webp";

            public static readonly string[] DefaultAllowDomains = [];

            public static readonly string[] DefaultAllowStyles = [];

            public static readonly string[] DefaultAllowFonts = ["fonts.gstatic.com", "fonts.intercomcdn.com", "js.intercomcdn.com", "data:"];

            public static readonly string[] DefaultAllowWorkers = [];

            public static readonly string[] DefaultAllowConnections = [];

            public static readonly string[] DefaultAllowFrames = [];

            public static readonly string[] DefaultAllowScripts = [];

            public static readonly string[] DefaultAllowImages = ["avatars.githubusercontent.com", "github.com"];

            public static readonly string[] DefaultAllowFormActions = [];

            public static readonly string[] DefaultAllowMedia = [];
        }
    }
}
