namespace UmbracoCommunity.Web.Features.Seed;

public interface ISeedExportService
{
    /// <summary>
    /// Returns the absolute path on disk to the latest snapshot zip, regardless of whether the file
    /// currently exists. Callers should check <see cref="File.Exists(string)"/> before serving.
    /// </summary>
    string GetLatestZipPath();

    /// <summary>
    /// Returns the current export status (last success/failure, in-progress).
    /// </summary>
    SeedExportStatus GetStatus();

    /// <summary>
    /// Regenerates the snapshot zip. Returns <c>false</c> if a regeneration is already in flight
    /// (caller should not retry immediately). Throws on unexpected failure; updates status on
    /// expected failures and returns <c>true</c>.
    /// </summary>
    Task<bool> RegenerateAsync(CancellationToken cancellationToken = default);
}
