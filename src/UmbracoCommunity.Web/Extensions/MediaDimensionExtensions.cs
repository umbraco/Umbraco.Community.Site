using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

namespace UmbracoCommunity.Web.Extensions;

public static class MediaDimensionExtensions
{
    public static (int Width, int Height)? GetDimensions(this MediaWithCrops? media)
    {
        if (media is null)
        {
            return null;
        }

        var width = media.Value<int>(Umbraco.Cms.Core.Constants.Conventions.Media.Width);
        var height = media.Value<int>(Umbraco.Cms.Core.Constants.Conventions.Media.Height);
        return width > 0 && height > 0 ? (width, height) : null;
    }

    public static int GetScaledHeight(this MediaWithCrops? media, int width)
        => media.GetDimensions() is { } size ? width * size.Height / size.Width : width;

    public static string? GetAspectRatioStyle(this MediaWithCrops? media)
        => media.GetDimensions() is { } size ? $"aspect-ratio: {size.Width} / {size.Height}" : null;

    public static (int Width, int Height)? GetCropSize(this MediaWithCrops? media, string alias)
    {
        var crop = media?.LocalCrops?.GetCrop(alias)
            ?? media?.Content.Value<ImageCropperValue>(Umbraco.Cms.Core.Constants.Conventions.Media.File)?.GetCrop(alias);
        return crop is { Width: > 0, Height: > 0 } ? (crop.Width, crop.Height) : null;
    }

    public static string? GetCropRatioStyle(this MediaWithCrops? media)
    {
        var ratios = new[] { "mobile", "desktop" }
            .Select(alias => (alias, size: media.GetCropSize(alias)))
            .Where(x => x.size is not null)
            .Select(x => $"--crop-ratio-{x.alias}: {x.size!.Value.Width} / {x.size.Value.Height}")
            .ToList();
        return ratios.Count > 0 ? string.Join("; ", ratios) : null;
    }
}
