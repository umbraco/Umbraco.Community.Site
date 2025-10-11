using System.Text.Json;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class NuGetApiClient
{
    private readonly HttpClient _httpClient;
    private const string NuGetApiUrl = "https://api.nuget.org/v3-flatcontainer";

    public NuGetApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Dictionary<string, DateTime>> GetPackageVersionsAsync(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return new Dictionary<string, DateTime>();
        }

        try
        {
            // Get all versions
            var versionsUrl = $"{NuGetApiUrl}/{packageId.ToLowerInvariant()}/index.json";
            var versionsResponse = await _httpClient.GetStringAsync(versionsUrl);
            var versionsDoc = JsonDocument.Parse(versionsResponse);
            var versions = versionsDoc.RootElement.GetProperty("versions").EnumerateArray()
                .Select(v => v.GetString()!)
                .ToList();

            var result = new Dictionary<string, DateTime>();

            // For each version, get the catalog entry which contains the published date
            foreach (var version in versions)
            {
                try
                {
                    var catalogUrl = $"https://api.nuget.org/v3/registration5-semver1/{packageId.ToLowerInvariant()}/{version.ToLowerInvariant()}.json";
                    var catalogResponse = await _httpClient.GetStringAsync(catalogUrl);
                    var catalogDoc = JsonDocument.Parse(catalogResponse);

                    if (catalogDoc.RootElement.TryGetProperty("published", out var publishedElement))
                    {
                        var publishedString = publishedElement.GetString();
                        if (!string.IsNullOrEmpty(publishedString) && DateTime.TryParse(publishedString, out var publishedDate))
                        {
                            result[version] = publishedDate;
                        }
                    }
                }
                catch
                {
                    // Skip versions we can't get dates for
                    continue;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching NuGet package versions for {packageId}: {ex.Message}");
            return new Dictionary<string, DateTime>();
        }
    }
}
