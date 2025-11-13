using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Helpers
{
    public static class ColourHelper
    {
        public static string? GetOptionalColor(this string? color) =>
            !string.IsNullOrEmpty(color) ? CreateHtmlColor(color) : null;

        public static string GetRequiredColor(this string? color, string defaultColor) =>
            !string.IsNullOrEmpty(color) && color != "#" ? CreateHtmlColor(color) : defaultColor;

        private static string CreateHtmlColor(string color) => color.StartsWith('#') ? color : "#" + color;

        public static bool IsDark(this ISettingsColour? colourSettings)
        {
            if (colourSettings?.BackgroundColour == null || string.IsNullOrWhiteSpace(colourSettings.BackgroundColour.Color)) return false;
            return ColourIsDark(colourSettings.BackgroundColour.Color);
        }

        public static bool IsDark(this string? colour)
        {
            if (string.IsNullOrWhiteSpace(colour)) return false;
            return ColourIsDark(colour);
        }

        public static bool HasBg(this ISettingsColour? colourSettings)
        {
            return colourSettings?.BackgroundColour != null &&
                !string.IsNullOrEmpty(colourSettings.BackgroundColour.Color) &&
                !string.Equals(colourSettings.BackgroundColour.Color, "#ffffff", StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool ColourIsDark(string colour)
        {
            var darkBgs = new[] { "#3544B1", "#1b264f" }; // dark blue, blue
            return darkBgs.Any(x => string.Equals(x, colour, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
