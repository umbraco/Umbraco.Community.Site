using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;

namespace UmbracoCommunity.Web.Models.Pages;

public class ComparePageViewModel : PageViewModelBase
{
    public ComparePageViewModel(IPublishedContent currentPage) : base(currentPage)
    {
        // Override to ensure the correct CSS entrypoint is loaded
        ContentTypeAlias = "releasesHome";
    }

    public List<ReleaseDiscussionViewModel> AvailableVersions { get; set; } = new();
    public string? FromVersion { get; set; }
    public string? ToVersion { get; set; }
    public string? LowestVersion { get; set; }
    public string? HighestVersion { get; set; }
    public List<VersionChangesGroup> VersionGroups { get; set; } = new();
    public string UmbracoLogoPath { get; set; } = "/img/umbraco_logo.png";
    public bool LabelCheck { get; set; }
    public bool IncludePreReleases { get; set; }
    public int FeatureCount { get; set; }
    public int BreakingChangesCount { get; set; }
    public int IssuesAndTasksCount { get; set; }
}

public class VersionChangesGroup
{
    public string Version { get; set; } = string.Empty;
    public List<ReleasePullRequestViewModel> Features { get; set; } = new();
    public List<ReleasePullRequestViewModel> BreakingChanges { get; set; } = new();
    public List<ReleasePullRequestViewModel> IssuesAndTasks { get; set; } = new();
}
