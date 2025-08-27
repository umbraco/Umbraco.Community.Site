using UmbracoCommunity.Web.Models.ViewModels.Properties;

namespace UmbracoCommunity.Web.Models.ViewModels.Blocks.Cards;

public class MultipleCardsItemBlockViewModel : BlockViewModelBase
{
    public required string Headline { get; init; }

    public string Text { get; init; } = string.Empty;

    public LinkViewModel? Link { get; init; }

    public ImageViewModel? Icon { get; init; }

    public bool HasIcon => Icon != null;

    public bool HasText => !string.IsNullOrEmpty(Text);

    public bool HasLink => Link is not null && !string.IsNullOrEmpty(Link.Url);

    public bool HasTable { get; set; } = false;
}
