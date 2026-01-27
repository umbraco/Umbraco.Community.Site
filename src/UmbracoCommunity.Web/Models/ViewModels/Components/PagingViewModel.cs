namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class PagingViewModel
{
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; } = 1;

    public int TotalItems { get; set; }

    public int PageSize { get; set; } = 10;

    public string? BaseUrl { get; set; }
}
