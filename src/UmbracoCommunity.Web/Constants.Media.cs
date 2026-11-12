namespace UmbracoCommunity.Web
{
    public static partial class Constants
    {
        public static class Media
        {
            public const long MaxHeroVideoBytes = 10 * 1024 * 1024;

            public static string FormatMegabytes(long bytes) => $"{bytes / 1024d / 1024d:0.#} MB";
        }
    }
}
