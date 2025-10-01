namespace UmbracoCommunity.Web.Models.ViewModels.Blocks;

public abstract class BlockViewModelBase
{
    public virtual string Alias => GetType().Name.Replace("ViewModel", string.Empty);
}
