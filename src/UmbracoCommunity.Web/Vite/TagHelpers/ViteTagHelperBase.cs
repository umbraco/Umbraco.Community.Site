using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Vite.Models;

namespace UmbracoCommunity.Web.Vite.TagHelpers;

/// <summary>
/// Tag helper adding Vite support for rendering script and link tags.
/// Hat tip => https://github.com/umbraco/Umbraco.Demo.Cloud/blob/master/Umbraco.Demo.Cloud.Core/Vite/TagHelpers/ViteTagHelperBase.cs
/// </summary>
/// <seealso cref=TagHelper" />
public abstract class ViteTagHelperBase : TagHelper
{
    protected const string ViteHrefAttributeName = "vite-href";
    protected const string ViteSrcAttributeName = "vite-src";

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebHostEnvironment _webHostEnvironment;

    private static IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());

    protected string? ViteManifestPath { get; }

    protected string? ViteDevServerUrl { get; }

    protected IUrlHelperFactory UrlHelperFactory { get; }

    /// <summary>
    /// Gets or sets the <see cref="Rendering.ViewContext" /> for the current request.
    /// </summary>
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViteTagHelperBase" /> class.
    /// </summary>
    public ViteTagHelperBase(IUrlHelperFactory urlHelperFactory, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
    {
        UrlHelperFactory = urlHelperFactory;
        _webHostEnvironment = webHostEnvironment;

        ViteManifestPath = configuration["ViteManifestPath"] ?? "/assets/manifest.json";
        ViteDevServerUrl = configuration["ViteDevServerUrl"] ?? "https://localhost:5123";
    }

    protected bool IsLocalEnvironment() => _webHostEnvironment.IsLocalEnvironment();

    /// <summary>
    /// Prefixes the entry with the correct static web assets base path
    /// </summary>
    /// <param name="entryName"></param>
    /// <returns></returns>
    protected string EntryNameWithBase(string entryName) => $"/assets/{entryName}";

    protected bool TryGetEntryName(string? contentPath, IUrlHelper urlHelper, [NotNullWhen(true)] out string? entryName)
    {
        entryName = default;

        if (contentPath is null)
        {
            return false;
        }

        string path = urlHelper.Content(contentPath).ToLowerInvariant();

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!path.StartsWith("/src/entrypoints/_"))
        {
            if (path.StartsWith('_'))
            {
                path = $"/src/entrypoints/{path}";
            }
            else
            {
                path = $"/src/entrypoints/_{path}";
            }
        }

        path = path.TrimStart('/');

        // from path, we want the extensionless path, to enable getting the correct entry from the manifest, where
        // the src extension is always .ts (and our request is for .js or .css, which are both provided in the same manifest entry
        // This means it's not necessary to include a file extension in the link/script tag, and can instead request the bundle by name only.
        if (Path.ChangeExtension(path, null) is string extensionlessPath)
        {
            entryName = $"{extensionlessPath}.ts";
            return true;
        }

        return false;
    }

    protected async Task<ViteManifest?> GetViteManifestAsync(IUrlHelper urlHelper)
    {
        string? viteManifestPath = urlHelper.Content(ViteManifestPath);

        if (viteManifestPath is null)
        {
            return default;
        }

        return await Cache.GetOrCreateAsync(viteManifestPath, async cacheEntry =>
        {
            // Expire when manifest is added, modified or deleted
            cacheEntry.AddExpirationToken(_webHostEnvironment.WebRootFileProvider.Watch(viteManifestPath));

            // Deserialize JSON if manifest file exists
            IFileInfo viteManifestFileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(viteManifestPath);
            if (viteManifestFileInfo.Exists)
            {
                using Stream stream = viteManifestFileInfo.CreateReadStream();
                return await JsonSerializer.DeserializeAsync<ViteManifest>(stream, _serializerOptions);
            }

            return null;
        });
    }
}
