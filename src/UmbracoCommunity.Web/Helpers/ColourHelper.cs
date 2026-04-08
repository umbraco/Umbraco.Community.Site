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
            var hex = colour.TrimStart('#');
            if (hex.Length is 6 or 8 &&
                int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                // Relative luminance (ITU-R BT.709)
                var luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
                return luminance < 0.5;
            }

            // Fallback: check known dark values
            var darkBgs = new[] { "#3544B1", "#1b264f" };
            return darkBgs.Any(x => string.Equals(x, colour, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
