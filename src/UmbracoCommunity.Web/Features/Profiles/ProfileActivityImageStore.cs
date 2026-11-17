using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.IO;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Keeps the pictures members attach to activity they add by hand (see <see cref="ProfileActivityEntry"/>). Pictures
/// live in the media file system (the same storage as uploaded avatars and <see cref="ProfileTimelineImageStore"/>'s
/// copies, so blob storage on Umbraco Cloud) under <see cref="Folder"/>, and are served from this site. A picture is
/// accepted only when it is at most <see cref="ProfileActivityEntry.MaxImageBytes"/> and its bytes are a JPEG, PNG, GIF
/// or WebP image (see <see cref="ProfileTimelineImageStore.Sniff"/>); whatever the upload calls itself is ignored. Each
/// is stored under a random, unguessable name with the extension of the format found, never the uploaded file name, so
/// a picture waiting for a check can't be found by anyone but the member and the reviewers.
/// </summary>
public sealed class ProfileActivityImageStore
{
    /// <summary>The folder in the media file system that holds the pictures.</summary>
    public const string Folder = "profile-activity";

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ProfileActivityImageStore> _logger;

    public ProfileActivityImageStore(IFileSystem fileSystem, ILogger<ProfileActivityImageStore> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Checks an uploaded picture and stores it. Returns where it is kept (for <see cref="Url"/> and <see cref="Delete"/>),
    /// or a message for the member when it isn't an accepted picture.
    /// </summary>
    /// <param name="length">The size the upload says it has; checked before anything is read.</param>
    public async Task<(string? Path, string? Error)> SaveAsync(Stream content, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0)
        {
            return (null, "The picture is empty. Choose another one.");
        }

        if (length > ProfileActivityEntry.MaxImageBytes)
        {
            return (null, TooLargeError);
        }

        var bytes = await ReadLimitedAsync(content, cancellationToken);
        if (bytes == null)
        {
            return (null, TooLargeError);
        }

        if (bytes.Length == 0 || ProfileTimelineImageStore.Sniff(bytes) is not { } format)
        {
            return (null, "The picture must be a JPEG, PNG, GIF or WebP image.");
        }

        var path = $"{Folder}/{FileName()}{format.Extension}";
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            _fileSystem.AddFile(path, stream, overrideIfExists: false);
        }

        return (path, null);
    }

    /// <summary>The address a stored picture is served at, on this site.</summary>
    public string Url(string path) => _fileSystem.GetUrl(path);

    /// <summary>Deletes a stored picture. A picture that is already gone, or can't be deleted, is logged and left; it never throws.</summary>
    public void Delete(string path)
    {
        try
        {
            if (_fileSystem.FileExists(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the activity picture {Path}", path);
        }
    }

    private static string TooLargeError => $"The picture must be {ProfileActivityEntry.MaxImageBytes / 1_000_000} MB or smaller.";

    /// <summary>A random name: 128 bits, as hex, so nothing from the upload reaches the path and it can't be guessed.</summary>
    private static string FileName() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    /// <summary>The content, or null when it is larger than <see cref="ProfileActivityEntry.MaxImageBytes"/> (whatever its length said).</summary>
    private static async Task<byte[]?> ReadLimitedAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new byte[ProfileActivityEntry.MaxImageBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await content.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total > ProfileActivityEntry.MaxImageBytes ? null : buffer[..total];
    }
}
