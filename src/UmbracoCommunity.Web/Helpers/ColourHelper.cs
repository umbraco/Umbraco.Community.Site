namespace UmbracoCommunity.Web.Helpers
{
    public static class ColourHelper
    {
        public static string? GetOptionalColor(this string? color) =>
            !string.IsNullOrEmpty(color) ? CreateHtmlColor(color) : null;

        public static string GetRequiredColor(this string? color, string defaultColor) =>
            !string.IsNullOrEmpty(color) && color != "#" ? CreateHtmlColor(color) : defaultColor;

        private static string CreateHtmlColor(string color) => color.StartsWith('#') ? color : "#" + color;

        public static bool IsDark(this string? colour)
        {
            var darkBgs = new[] { "#3544B1", "#8E755E" }; // dark blue, brown
            return darkBgs.Any(x => string.Equals(x, colour, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
