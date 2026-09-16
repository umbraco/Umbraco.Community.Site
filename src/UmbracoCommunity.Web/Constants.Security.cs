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

            // Two different Umbraco server-side tagging hosts, despite the names — do not assume one covers the
            // other. load.sst.umbraco.com serves the GTM container the inline loader requests (script-src), while
            // sst.umbraco.com is where the Stape Data Tag POSTs its payload (connect-src): tag_id 582 fires a
            // page_view on every page with no consent gate, so a missing sst.umbraco.com is a guaranteed console
            // error on every request, not an edge case.
            //
            // Adding a host here is only needed for destinations that cannot carry a nonce — fetch/XHR targets and
            // img pixels. GTM propagates this page's nonce to the scripts it injects (that is what the data-nonce
            // attribute on #gtmScript is for, see MetaTags.cshtml), so those satisfy script-src by nonce whatever
            // their host: stapecdn.com and consent.cookiebot.com both load today without appearing in any list.
            // Individual GTM tags may still want further connect/img hosts — read the console's CSP violations and
            // add exactly those rather than widening pre-emptively. The consent-gated tags (LinkedIn, Navattic)
            // have never fired, because the Cookiebot banner is not authorised for these domains yet.
            public static readonly string[] DefaultAllowConnections = ["load.sst.umbraco.com", "sst.umbraco.com", "consentcdn.cookiebot.com"];

            // consentcdn.cookiebot.com is framed by the Cookiebot consent banner, which GTM loads. Without it the
            // iframe is blocked and the banner's postMessage handshake fails with a "target origin ... does not
            // match" error against origin 'null' — a symptom of the blocked frame, not a separate fault.
            public static readonly string[] DefaultAllowFrames = ["*.youtube.com", "*.walls.io", "consentcdn.cookiebot.com"];

            public static readonly string[] DefaultAllowScripts = ["*.youtube.com", "load.sst.umbraco.com"];

            public static readonly string[] DefaultAllowImages = ["avatars.githubusercontent.com", "github.com", "api.dicebear.com", "images.pexels.com", "data:", "sessionize.com", "cache.sessionize.com", "assets.sessionize.com", "cdn.sessionize.com"];

            public static readonly string[] DefaultAllowFormActions = ["github.com"];

            public static readonly string[] DefaultAllowMedia = [];
        }
    }
}
