namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseCardViewModel
{
    public ReleaseInfoViewModel Release { get; set; } = null!;
    public string CardType { get; set; } = "lts"; // "lts" or "latest"
    public bool ShowBadge { get; set; } = true; // Show badge to differentiate between LTS and Latest
}
