using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class VideoShowcaseBlock
    {
        public const int MaxVideos = 12;

        public string IdHash { get; } = "vs-" + StringUtilities.RandomString(5);

        public bool IsGrid(SettingsVideoShowcaseBlock? settings) =>
            string.Equals(settings?.Layout, "grid", StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<VideoShowcaseItem> PlayableVideos =>
            Videos?.Select(block => block.Content)
                .OfType<VideoShowcaseItem>()
                .Where(item => item.Video is not null)
                .Take(MaxVideos)
                .ToList() ?? [];
    }

    public partial class VideoShowcaseItem
    {
        public string? Byline =>
            string.Join(" · ", new[] { Speaker, Edition }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } byline
                ? byline
                : null;
    }
}
