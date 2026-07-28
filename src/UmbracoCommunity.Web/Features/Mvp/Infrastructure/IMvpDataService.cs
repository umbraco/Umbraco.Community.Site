using UmbracoCommunity.Web.Features.Mvp.Models;

namespace UmbracoCommunity.Web.Features.Mvp.Infrastructure;

public interface IMvpDataService
{
    IReadOnlyList<MvpYear> GetAll();

    MvpYear? GetLatest();
}
