namespace UmbracoCommunity.Extensions.Features.GitHub.Models;

public class SampleDataImportResult
{
    public int HqMembersAdded { get; set; }
    public int HqMembersUpdated { get; set; }
    public int IssuesAdded { get; set; }
    public int IssuesUpdated { get; set; }
    public int PullRequestsAdded { get; set; }
    public int PullRequestsUpdated { get; set; }
    public int DiscussionsAdded { get; set; }
    public int DiscussionsUpdated { get; set; }
    public int NuGetPackagesAdded { get; set; }
    public int NuGetPackagesUpdated { get; set; }

    public int TotalAdded => HqMembersAdded + IssuesAdded + PullRequestsAdded + DiscussionsAdded + NuGetPackagesAdded;
    public int TotalUpdated => HqMembersUpdated + IssuesUpdated + PullRequestsUpdated + DiscussionsUpdated + NuGetPackagesUpdated;
}
