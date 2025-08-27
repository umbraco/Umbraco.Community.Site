using UmbracoDotCom.Common.Utilities;

namespace UmbracoCommunity.Web.Models.ViewModels.Blocks;

public abstract class BlockViewModelBase
{
    public virtual string Alias => GetType().Name.Replace("ViewModel", string.Empty);

    public virtual int GetWordCount() => 0;

    public string IdHash { get; } = StringUtilities.RandomString(5);
}
