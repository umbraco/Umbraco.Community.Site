using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Utilities;

/// <summary>
/// Helpers for writing disk-cache files that may be touched by more than one process at once.
/// </summary>
/// <remarks>
/// On Umbraco Cloud (Azure App Service), a recycle briefly runs the old and new instances side by side.
/// During that overlap both instances' background jobs write the same cache file, which used to surface as
/// <see cref="IOException"/> ("being used by another process") and <see cref="UnauthorizedAccessException"/>
/// ("access to the path is denied") in the logs. Writing via a per-write unique temp file and moving it into
/// place — retrying the move through the transient Windows lock a reader can hold — makes the write both atomic
/// for readers and safe against a concurrent writer. Contention is expected and benign, so it is logged at Debug.
/// </remarks>
internal static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="contents"/> to <paramref name="path"/> atomically, tolerating a concurrent
    /// writer/reader on the same file. Never throws; failures are swallowed after logging.
    /// </summary>
    public static async Task WriteAllTextAsync(
        string path,
        string contents,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // Unique per write so two instances never collide on the temp file itself.
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, contents, cancellationToken);

            // Move is atomic on the same volume: a concurrent reader sees either the old file or the fully
            // written new one, never a partial write. The overwrite can still lose a brief race with a reader
            // holding the destination on Windows, so retry a few times before giving up.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(tempPath, path, overwrite: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 3)
                {
                    await Task.Delay(50 * attempt, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            // Another instance almost certainly wrote an equally fresh copy, so this is not worth a warning.
            logger.LogDebug(ex, "Could not write disk cache to {Path} (likely concurrent write during a recycle).", path);
        }
        finally
        {
            TryDeleteTemp(tempPath);
        }
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Orphaned temp files sit in umbraco/Data/TEMP and are harmless; ignore.
        }
    }
}
