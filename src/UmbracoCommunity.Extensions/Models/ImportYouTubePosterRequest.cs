namespace UmbracoCommunity.Extensions.Models;

/// <param name="Url">The YouTube link the editor pasted.</param>
/// <param name="Variant">Which of the preview's stills to import, e.g. <c>maxres2</c>.</param>
/// <param name="ContentKey">The page being edited (or its parent while it's unsaved), which decides the site folder.</param>
/// <param name="Title">The video title, used to name the media item.</param>
public sealed record ImportYouTubePosterRequest(string Url, string Variant, Guid? ContentKey, string? Title);
