using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// The MVP badge artwork, one PNG per year with the year drawn in. Shipped as embedded resources named
/// <c>Features/OpenBadges/Assets/mvp-{year}.png</c>; a year without a file has no badge. The images are resized to
/// <see cref="OpenBadgesOptions.ImageSize"/> once and kept in memory.
/// </summary>
public sealed class MvpBadgeArtwork
{
    private const string ResourcePrefix = "UmbracoCommunity.Web.Features.OpenBadges.Assets.mvp-";

    private readonly Assembly _assembly;
    private readonly int _imageSize;
    private readonly ConcurrentDictionary<int, Lazy<byte[]?>> _images = new();

    public MvpBadgeArtwork(IOptions<OpenBadgesOptions> options)
        : this(typeof(MvpBadgeArtwork).Assembly, options.Value.ImageSize)
    {
    }

    internal MvpBadgeArtwork(Assembly assembly, int imageSize)
    {
        _assembly = assembly;
        _imageSize = imageSize;
    }

    public bool Exists(int year) => _assembly.GetManifestResourceInfo(ResourceName(year)) is not null;

    /// <summary>The badge image for <paramref name="year"/> as PNG bytes, or null when there is no artwork for it.</summary>
    public byte[]? GetPng(int year) =>
        Exists(year) ? _images.GetOrAdd(year, y => new Lazy<byte[]?>(() => Load(y))).Value : null;

    private byte[]? Load(int year)
    {
        using var stream = _assembly.GetManifestResourceStream(ResourceName(year));
        if (stream is null)
        {
            return null;
        }

        using var image = Image.Load(stream);
        if (_imageSize > 0 && (image.Width != _imageSize || image.Height != _imageSize))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(_imageSize, _imageSize),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3,
            }));
        }

        using var output = new MemoryStream();
        image.Save(output, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
        return output.ToArray();
    }

    private static string ResourceName(int year) => $"{ResourcePrefix}{year}.png";
}
