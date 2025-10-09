namespace UmbracoCommunity.Web.Models.Releases;

public class ReleasePullRequestViewModel
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string AuthorLogin { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public string? AuthorUrl { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Labels { get; set; } = new();
    public bool IsHqMember { get; set; }
    public bool IsFirstTimeContributor { get; set; }
    public string? MergedByLogin { get; set; }
    public string? MergedByName { get; set; }
    public string? MergedByUrl { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsIssue => Url.Contains("/issues/");
    public bool IsMerged => MergedByLogin != null;
    public bool HasStateLabel => Labels.Any(l => l.StartsWith("state/"));
    public bool HasAffectedLabel => Labels.Any(l => l.StartsWith("affected/"));
    public bool HasCommunityPrLabel => Labels.Contains("community/pr");
    public bool NeedsAttention =>
        (IsIssue && (State != "closed" || HasStateLabel || HasAffectedLabel)) ||
        (!IsIssue && !IsMerged) ||
        (!IsIssue && !IsHqMember && !HasCommunityPrLabel);

    public string? AlertMessage
    {
        get
        {
            if (IsIssue)
            {
                if (!State.Equals("closed", StringComparison.OrdinalIgnoreCase))
                    return "Open";
                if (HasStateLabel || HasAffectedLabel)
                    return "Incorrect labeling";
            }
            else // Is PR
            {
                if (!IsMerged)
                    return "Not merged";
                if (!IsHqMember && !HasCommunityPrLabel)
                    return "Incorrect labeling";
            }
            return null;
        }
    }
}
