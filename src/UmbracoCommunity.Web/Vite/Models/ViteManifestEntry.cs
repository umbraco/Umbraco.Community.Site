namespace UmbracoCommunity.Web.Vite.Models;
public sealed class ViteManifestEntry
{
    public string File { get; set; } = null!;

    public string? Src { get; set; }

    public bool IsEntry { get; set; }

    public string[]? Imports { get; set; }

    public string[]? DynamicImports { get; set; }

    public string[]? Css { get; set; }

    public string[]? Assets { get; set; }
}
