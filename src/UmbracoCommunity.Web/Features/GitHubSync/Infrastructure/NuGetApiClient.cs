using System.Text.Json;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class NuGetApiClient
{
    private readonly HttpClient _httpClient;

    public NuGetApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Dictionary<string, DateTime>> GetPackageVersionsAsync(string packageId, int? maxCount = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return new Dictionary<string, DateTime>();
        }

        try
        {
            var baseUrl = $"https://api.nuget.org/v3/registration5-gz-semver2/{packageId.ToLower()}/index.json";
            var indexResponse = await _httpClient.GetStringAsync(baseUrl);
            var indexDoc = JsonDocument.Parse(indexResponse);

            var allVersions = new List<(string Version, DateTime Published)>();

            // Process all pages
            if (indexDoc.RootElement.TryGetProperty("items", out var pagesElement))
            {
                foreach (var page in pagesElement.EnumerateArray())
                {
                    List<JsonElement> items;

                    // Check if items are inlined in the page
                    if (page.TryGetProperty("items", out var inlineItems))
                    {
                        items = inlineItems.EnumerateArray().ToList();
                    }
                    else if (page.TryGetProperty("@id", out var pageIdElement))
                    {
                        // Items are not inlined, fetch the page separately
                        var pageUrl = pageIdElement.GetString();
                        if (string.IsNullOrEmpty(pageUrl))
                            continue;

                        var pageResponse = await _httpClient.GetStringAsync(pageUrl);
                        var pageDoc = JsonDocument.Parse(pageResponse);

                        if (pageDoc.RootElement.TryGetProperty("items", out var pageItems))
                        {
                            items = pageItems.EnumerateArray().ToList();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    // Extract version and published date from each item
                    foreach (var item in items)
                    {
                        if (item.TryGetProperty("catalogEntry", out var catalogEntry))
                        {
                            var version = catalogEntry.TryGetProperty("version", out var versionElement)
                                ? versionElement.GetString()
                                : null;

                            var publishedString = catalogEntry.TryGetProperty("published", out var publishedElement)
                                ? publishedElement.GetString()
                                : null;

                            if (!string.IsNullOrEmpty(version) &&
                                !string.IsNullOrEmpty(publishedString) &&
                                DateTime.TryParse(publishedString, out var publishedDate))
                            {
                                allVersions.Add((version, publishedDate));
                            }
                        }
                    }
                }
            }

            // Sort by published date descending and apply max count if specified
            var sortedVersions = allVersions.OrderByDescending(v => v.Published);

            var finalVersions = maxCount.HasValue
                ? sortedVersions.Take(maxCount.Value)
                : sortedVersions;

            return finalVersions.ToDictionary(v => v.Version, v => v.Published);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching NuGet package versions for {packageId}: {ex.Message}");
            return new Dictionary<string, DateTime>();
        }
    }
}
