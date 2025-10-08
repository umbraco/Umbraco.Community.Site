namespace UmbracoCommunity.Web.Models.ViewModels.Properties;

public class ImageViewModel
{
    public ImageViewModel(string url, string? altText)
    {
        Url = url;
        AltText = altText ?? string.Empty;
    }

    public string Url { get; }

    public string AltText { get; }

    public static ImageViewModel Empty => new(string.Empty, string.Empty);
}
