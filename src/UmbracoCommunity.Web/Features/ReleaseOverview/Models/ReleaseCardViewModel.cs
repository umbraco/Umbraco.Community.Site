namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseCardViewModel
{
    public ReleaseInfoViewModel Release { get; set; } = null!;
    public string CardType { get; set; } = "lts"; // "lts" or "latest"
}
