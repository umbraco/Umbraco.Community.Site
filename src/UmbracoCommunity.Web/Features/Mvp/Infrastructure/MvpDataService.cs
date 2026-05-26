using System.Text.Json;
using UmbracoCommunity.Web.Features.Mvp.Models;

namespace UmbracoCommunity.Web.Features.Mvp.Infrastructure;

internal sealed class MvpDataService : IMvpDataService
{
    private const string ResourceName = "UmbracoCommunity.Web.Features.Mvp.Data.mvp.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyList<MvpYear> _years;

    public MvpDataService()
    {
        var assembly = typeof(MvpDataService).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found in {assembly.FullName}. " +
                "Check the <EmbeddedResource> include in UmbracoCommunity.Web.csproj.");

        var raw = JsonSerializer.Deserialize<RawYear[]>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' deserialised to null.");

        _years = raw
            .OrderByDescending(y => y.Year)
            .Select(MapYear)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<MvpYear> GetAll() => _years;

    public MvpYear? GetLatest() => _years.Count > 0 ? _years[0] : null;

    private static MvpYear MapYear(RawYear y) => new(
        y.Year,
        (y.Members ?? Array.Empty<RawMember>())
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapMember)
            .ToList()
            .AsReadOnly());

    private static MvpMember MapMember(RawMember m)
    {
        var handle = Normalise(m.GitHub);
        var (url, srcset) = ResolveAvatar(m, handle);
        return new MvpMember(m.Id, m.Name, m.IsMvpRenewal, url, srcset, handle);
    }

    /// <summary>
    /// Decide which avatar source to use. GitHub handle present? Hot-link to
    /// github.com/&lt;handle&gt;.png — always current, browser caches per URL.
    /// Otherwise generate a deterministic abstract avatar from DiceBear seeded
    /// by the member's Id, so the same person gets the same placeholder every
    /// time without needing a real photo on file.
    /// </summary>
    private static (string Url, string? Srcset) ResolveAvatar(RawMember m, string? handle)
    {
        if (handle is not null)
        {
            var lc = handle.ToLowerInvariant();
            return (
                $"https://github.com/{lc}.png?size=200",
                $"https://github.com/{lc}.png?size=400 2x");
        }

        // SVG scales freely, so a single source is fine — no srcset needed.
        return ($"https://api.dicebear.com/9.x/shapes/svg?seed={m.Id}&size=200", null);
    }

    private static string? Normalise(string? handle) =>
        string.IsNullOrWhiteSpace(handle) ? null : handle!.Trim();

    private sealed record RawYear(int Year, RawMember[]? Members);
    private sealed record RawMember(int Id, string Name, bool IsMvpRenewal, string? GitHub);
}
