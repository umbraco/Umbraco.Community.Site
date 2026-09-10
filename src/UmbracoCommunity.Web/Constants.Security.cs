namespace UmbracoCommunity.Web
{
    public static partial class Constants
    {
        public static class Security
        {
            // Standard web photo formats only — TIFF is excluded because most browsers
            // don't render it in an <img> tag, so a TIFF avatar would upload successfully
            // but never actually display.
            public const string AllowedImageTypes = "gif,jpg,jpeg,png,webp";

            public static readonly string[] DefaultAllowDomains = [];

            public static readonly string[] DefaultAllowStyles = [];

            public static readonly string[] DefaultAllowFonts = ["fonts.gstatic.com", "data:"];

            public static readonly string[] DefaultAllowWorkers = [];

            // load.sst.umbraco.com is Umbraco's server-side tagging endpoint: the inline GTM loader fetches its
            // script from there, and the tags it runs beacon data back to the same host. Both directives are
            // needed or the loader 404s on CSP instead of doing anything. Individual GTM tags may want further
            // hosts — read the console's CSP violations and add exactly those rather than widening pre-emptively.
            public static readonly string[] DefaultAllowConnections = ["load.sst.umbraco.com"];

            public static readonly string[] DefaultAllowFrames = ["*.youtube.com", "*.walls.io"];

            public static readonly string[] DefaultAllowScripts = ["*.youtube.com", "load.sst.umbraco.com"];

            public static readonly string[] DefaultAllowImages = ["avatars.githubusercontent.com", "github.com", "api.dicebear.com", "images.pexels.com", "data:", "sessionize.com", "cache.sessionize.com", "assets.sessionize.com", "cdn.sessionize.com"];

            public static readonly string[] DefaultAllowFormActions = ["github.com"];

            public static readonly string[] DefaultAllowMedia = [];
        }
    }
}
