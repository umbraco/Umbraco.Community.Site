using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Core.Connectors.ServiceConnectors;
using Umbraco.Deploy.Infrastructure;
using Umbraco.Deploy.Infrastructure.Extensions;

namespace UmbracoCommunity.Web.Features.Seed;

public sealed class SeedExportService : ISeedExportService
{
    /// <summary>The Community root content node — root of the export.</summary>
    private static readonly Guid CommunityRootKey = Guid.Parse("1fb7669a-3ec3-462a-a054-08d640d20f30");

    /// <summary>Matches the IncludeDependencies value the backoffice UI uses (Content + ContentFile).</summary>
    private const DeployEntityTypeCategories IncludeDependenciesFlags = DeployEntityTypeCategories.ContentAndFiles;

    private const string ExportSubdirectory = "umbraco/Deploy/Exports";
    private const string ZipFileName = "import-on-startup.latest.zip";
    private const string PartialFileName = "import-on-startup.latest.zip.partial";

    private readonly IArtifactImportExportService _artifactImportExportService;
    private readonly IServiceConnectorFactory _serviceConnectorFactory;
    private readonly IFileTypeCollection _fileTypeCollection;
    private readonly IHostingEnvironment _hostingEnvironment;
    private readonly ILogger<SeedExportService> _logger;
    private readonly TimeProvider _time;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SeedExportStatus _status = new(
        IsRunning: false,
        LastSuccessAt: null,
        LastSuccessSizeBytes: null,
        LastFailureAt: null,
        LastError: null,
        StartedAt: null);

    public SeedExportService(
        IArtifactImportExportService artifactImportExportService,
        IServiceConnectorFactory serviceConnectorFactory,
        IFileTypeCollection fileTypeCollection,
        IHostingEnvironment hostingEnvironment,
        ILogger<SeedExportService> logger,
        TimeProvider time)
    {
        _artifactImportExportService = artifactImportExportService;
        _serviceConnectorFactory = serviceConnectorFactory;
        _fileTypeCollection = fileTypeCollection;
        _hostingEnvironment = hostingEnvironment;
        _logger = logger;
        _time = time;
    }

    public string GetLatestZipPath() =>
        Path.Combine(_hostingEnvironment.MapPathContentRoot("~/" + ExportSubdirectory), ZipFileName);

    public SeedExportStatus GetStatus() => _status;

    public async Task<bool> RegenerateAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var startedAt = _time.GetUtcNow();
        _status = _status with { IsRunning = true, StartedAt = startedAt };

        try
        {
            var directory = _hostingEnvironment.MapPathContentRoot("~/" + ExportSubdirectory);
            Directory.CreateDirectory(directory);

            var finalPath = Path.Combine(directory, ZipFileName);
            var partialPath = Path.Combine(directory, PartialFileName);

            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }

            var udi = new GuidUdi(Umbraco.Cms.Core.Constants.UdiEntityType.Document, CommunityRootKey);
            var dependencyEntityTypes = DeployEntityTypes.GetEntityTypes(_fileTypeCollection, IncludeDependenciesFlags);

            _logger.LogInformation(
                "Starting seed export for {Udi} (selector: {Selector}, deps: {Deps})",
                udi, Umbraco.Cms.Core.Constants.DeploySelector.ThisAndDescendants, IncludeDependenciesFlags);

            using (var zip = ZipFile.Open(partialPath, ZipArchiveMode.Create))
            {
                var contextCache = new DictionaryCache();
                await _artifactImportExportService.ExportArtifactsAsync(
                    _serviceConnectorFactory,
                    new[] { (Udi)udi },
                    Umbraco.Cms.Core.Constants.DeploySelector.ThisAndDescendants,
                    contextCache,
                    zip,
                    progress: null,
                    cancellationToken,
                    dependencyEntityTypes).ConfigureAwait(false);
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(partialPath, finalPath);

            var size = new FileInfo(finalPath).Length;
            var finishedAt = _time.GetUtcNow();

            _logger.LogInformation(
                "Seed export complete: {Bytes:N0} bytes in {Duration} at {Path}",
                size, finishedAt - startedAt, finalPath);

            _status = new SeedExportStatus(
                IsRunning: false,
                LastSuccessAt: finishedAt,
                LastSuccessSizeBytes: size,
                LastFailureAt: _status.LastFailureAt,
                LastError: _status.LastError,
                StartedAt: null);

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Seed export cancelled.");
            _status = _status with { IsRunning = false, StartedAt = null };
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seed export failed.");
            _status = new SeedExportStatus(
                IsRunning: false,
                LastSuccessAt: _status.LastSuccessAt,
                LastSuccessSizeBytes: _status.LastSuccessSizeBytes,
                LastFailureAt: _time.GetUtcNow(),
                LastError: ex.Message,
                StartedAt: null);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
