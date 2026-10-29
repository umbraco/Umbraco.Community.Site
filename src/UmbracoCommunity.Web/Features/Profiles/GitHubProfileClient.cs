using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The outcome of a GitHub profile lookup. <see cref="Succeeded"/> is false when GitHub could
/// not be reached or answered with an error, so the lookup can be tried again later.
/// </summary>
public sealed record GitHubProfileResult(bool Succeeded, string? Bio, string? Company)
{
    public static GitHubProfileResult Failure() => new(false, null, null);
    public static GitHubProfileResult Success(string? bio, string? company) => new(true, bio, company);
}

/// <summary>
/// Reads a member's bio and company from the public GitHub users API
/// (<c>https://api.github.com/users/{handle}</c>), in one request. Both are trimmed and cut to the
/// bio and headline limits; the company's leading '@' (GitHub's organisation mention) is removed.
/// Best-effort: any failure is logged and returned as <see cref="GitHubProfileResult.Failure"/>, never thrown.
/// The <see cref="HttpClient"/> is configured in <see cref="RegisterMemberProfiles"/>.
/// </summary>
public class GitHubProfileClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubProfileClient> _logger;

    public GitHubProfileClient(HttpClient httpClient, ILogger<GitHubProfileClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GitHubProfileResult> GetProfileAsync(string handle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return GitHubProfileResult.Failure();
        }

        try
        {
            using var response = await _httpClient.GetAsync($"users/{Uri.EscapeDataString(handle)}", cancellationToken);

            // An unknown user has nothing to use; that is an answer, not a failure.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return GitHubProfileResult.Success(null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub profile lookup for {Handle} returned {StatusCode}", handle, (int)response.StatusCode);
                return GitHubProfileResult.Failure();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var bio = ReadString(document.RootElement, "bio");
            var company = ReadString(document.RootElement, "company")?.Trim().TrimStart('@');

            return GitHubProfileResult.Success(
                ProfileText.NormalizeAndTruncate(bio, ProfileText.MaxBioLength),
                ProfileText.NormalizeAndTruncate(company, ProfileText.MaxHeadlineLength));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "GitHub profile lookup for {Handle} failed", handle);
            return GitHubProfileResult.Failure();
        }
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var element)
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
