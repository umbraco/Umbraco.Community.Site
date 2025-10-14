namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseCardViewModel
{
    public ReleaseDiscussionViewModel Release { get; set; } = null!;
    public string CardType { get; set; } = "lts"; // "lts" or "latest"
}
