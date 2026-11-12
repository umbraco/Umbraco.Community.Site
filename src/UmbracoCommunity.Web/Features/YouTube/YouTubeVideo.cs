using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Helpers;

namespace UmbracoCommunity.Web.Features.YouTube;

/// <summary>A YouTube video picked in the backoffice, with its poster imported into the Media library.</summary>
public sealed record YouTubeVideo(string Url, string VideoId, string? Title, IPublishedContent? Poster)
{
    /// <summary>The privacy-enhanced embed URL, keeping any start time from the pasted link.</summary>
    public string EmbedUrl => Url.GetYouTubeLink(null) is { Length: > 0 } embed
        ? embed
        : $"https://{VideoHelper.YouTubeEmbedHost}/embed/{VideoId}";

    public string WatchUrl => $"https://www.youtube.com/watch?v={VideoId}";
}
