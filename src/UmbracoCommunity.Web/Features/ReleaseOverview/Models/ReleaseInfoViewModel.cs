namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseInfoViewModel
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseLabel { get; set; } = string.Empty;
    public string? ActualLatestVersion { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public bool IsReleaseDateTba { get; set; }
    public bool IsLts { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsAvailableOnNuGet { get; set; }
    public bool IsReleased => IsAvailableOnNuGet;
    public string DiscussionUrl { get; set; } = string.Empty;
    public bool HasPreRelease { get; set; }
    public string? PreReleaseVersion { get; set; }
    public string FormattedReleaseDate
    {
        get
        {
            if (IsReleaseDateTba)
                return "To be announced";
            if (ReleaseDate.HasValue)
                return ReleaseDate.Value.ToString("dddd, MMMM d yyyy");
            return "To be announced";
        }
    }

    public string DisplayVersion => ActualLatestVersion ?? Version;
}
