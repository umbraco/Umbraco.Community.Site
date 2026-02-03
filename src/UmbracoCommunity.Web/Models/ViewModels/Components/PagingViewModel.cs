namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class PagingViewModel
{
    public PagingViewModel(int pageNumber, int pageSize, int totalItemCount)
    {
        CurrentPage = pageNumber;
        PageSize = pageSize;
        TotalItems = totalItemCount;
    }

    public PagingViewModel(int pageNumber, int pageSize, int totalItemCount, string additionalQueryString)
        : this(pageNumber, pageSize, totalItemCount) => AdditionalQueryString = additionalQueryString;

    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; } = 1;

    public int TotalItems { get; set; }

    public int PageSize { get; set; } = 10;

    public bool HasPrevious => CurrentPage > 1;

    public bool HasNext => CurrentPage < CurrentPage;

    public string AdditionalQueryString { get; } = string.Empty;
}
