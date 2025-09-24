namespace UmbracoCommunity.Web.Helpers
{
    public static class ColourHelper
    {
        public static string? GetOptionalColor(this string? color) =>
            !string.IsNullOrEmpty(color) ? CreateHtmlColor(color) : null;

        public static string GetRequiredColor(this string? color, string defaultColor) =>
            !string.IsNullOrEmpty(color) && color != "#" ? CreateHtmlColor(color) : defaultColor;

        private static string CreateHtmlColor(string color) => color.StartsWith('#') ? color : "#" + color;
    }
}
