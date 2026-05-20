using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Services.Documentation;

public sealed class DocumentationService : IDocumentationService, IDisposable
{
    /// <summary>
    /// Placeholder substituted by the view model builder with the resolved Documentation node URL,
    /// so the index stays per-tenant agnostic. URL-safe so Markdig's HTML renderer doesn't escape it.
    /// </summary>
    public const string DocBaseToken = "/__docbase__";

    private static readonly string[] TopLevelSections = ["primers", "tutorials"];
    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "IDEAS.md",
    };

    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentationService> _logger;
    private readonly MarkdownPipeline _pipeline;
    private readonly object _swapLock = new();

    private Lazy<DocumentationIndex> _index;
    private FileSystemWatcher? _watcher;
    private string? _resolvedRoot;
    private string? _repositoryUrl;

    public DocumentationService(
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        ILogger<DocumentationService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers()
            .Build();

        _index = new Lazy<DocumentationIndex>(BuildIndex, isThreadSafe: true);
    }

    public DocumentationIndex GetIndex() => _index.Value;

    public DocumentationResolution Resolve(IReadOnlyList<string> pathSegments)
    {
        var index = GetIndex();
        if (pathSegments.Count == 0)
        {
            return DocumentationResolution.NotFound;
        }

        var firstSegment = pathSegments[0];
        var section = index.Sections.FirstOrDefault(s =>
            string.Equals(s.Slug, firstSegment, StringComparison.OrdinalIgnoreCase));

        if (section is null)
        {
            return DocumentationResolution.NotFound;
        }

        return ResolveWithin(section, pathSegments, depth: 1);
    }

    private static DocumentationResolution ResolveWithin(
        DocumentationSection section,
        IReadOnlyList<string> pathSegments,
        int depth)
    {
        if (depth == pathSegments.Count)
        {
            return new DocumentationResolution(section, null);
        }

        var nextSegment = pathSegments[depth];

        var subsection = section.Subsections.FirstOrDefault(s =>
            string.Equals(s.Slug, nextSegment, StringComparison.OrdinalIgnoreCase));
        if (subsection is not null)
        {
            return ResolveWithin(subsection, pathSegments, depth + 1);
        }

        // Article must be the final segment.
        if (depth == pathSegments.Count - 1)
        {
            var article = section.Articles.FirstOrDefault(a =>
                string.Equals(a.Slug, nextSegment, StringComparison.OrdinalIgnoreCase));
            if (article is not null)
            {
                return new DocumentationResolution(null, article);
            }
        }

        return DocumentationResolution.NotFound;
    }

    private DocumentationIndex BuildIndex()
    {
        var root = ResolveDocsRoot();
        if (root is null || !Directory.Exists(root))
        {
            _logger.LogInformation("DocumentationService: no docs root resolved; index will be empty.");
            return DocumentationIndex.Empty;
        }

        _resolvedRoot = root;
        _repositoryUrl = _configuration["Documentation:RepositoryUrl"]?.TrimEnd('/');
        EnsureWatcher(root);

        var sections = new List<DocumentationSection>();
        foreach (var sectionName in TopLevelSections)
        {
            var sectionDir = Path.Combine(root, sectionName);
            if (!Directory.Exists(sectionDir))
            {
                continue;
            }

            var built = BuildSection(sectionDir, [sectionName], sectionName);
            if (!built.IsEmpty)
            {
                sections.Add(built);
            }
        }

        _logger.LogInformation("DocumentationService: indexed {SectionCount} sections from {Root}.", sections.Count, root);
        return new DocumentationIndex(sections);
    }

    private DocumentationSection BuildSection(string directory, IReadOnlyList<string> pathSegments, string slug)
    {
        var articles = new List<DocumentationArticle>();
        var subsections = new List<DocumentationSection>();
        DocumentationArticle? intro = null;

        foreach (var file in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file);
            if (ExcludedFileNames.Contains(fileName))
            {
                continue;
            }

            var article = BuildArticle(file, pathSegments);
            if (article is null)
            {
                continue;
            }

            if (string.Equals(fileName, "README.md", StringComparison.OrdinalIgnoreCase))
            {
                intro = article with { IsSectionIntro = true };
            }
            else
            {
                articles.Add(article);
            }
        }

        foreach (var subdir in Directory.EnumerateDirectories(directory)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var subSlug = Path.GetFileName(subdir);
            var subPath = pathSegments.Concat(new[] { subSlug }).ToArray();
            var sub = BuildSection(subdir, subPath, subSlug);
            if (!sub.IsEmpty)
            {
                subsections.Add(sub);
            }
        }

        var title = intro?.Title ?? Humanise(slug);
        var url = "/" + string.Join("/", pathSegments);
        return new DocumentationSection(title, slug, pathSegments, url, subsections, articles, intro);
    }

    private DocumentationArticle? BuildArticle(string filePath, IReadOnlyList<string> sectionPathSegments)
    {
        try
        {
            var raw = File.ReadAllText(filePath);
            var document = Markdown.Parse(raw, _pipeline);

            // Extract title from first H1 then strip it from the rendered output.
            var (title, h1Block) = ExtractTitleAndHeading(document);
            if (h1Block is not null)
            {
                document.Remove(h1Block);
            }

            // Rewrite cross-doc markdown links to absolute documentation URLs.
            RewriteInternalLinks(document, filePath);

            var html = RenderToHtml(document);

            var excerpt = ExtractExcerpt(document);

            var slug = Path.GetFileNameWithoutExtension(filePath);
            var isReadme = string.Equals(Path.GetFileName(filePath), "README.md", StringComparison.OrdinalIgnoreCase);
            var pathSegments = isReadme
                ? sectionPathSegments
                : sectionPathSegments.Concat(new[] { slug }).ToArray();

            var url = "/" + string.Join("/", pathSegments);

            return new DocumentationArticle(
                Title: string.IsNullOrWhiteSpace(title) ? Humanise(slug) : title!,
                Slug: isReadme ? sectionPathSegments[^1] : slug,
                PathSegments: pathSegments,
                Url: url,
                HtmlContent: html,
                Excerpt: excerpt,
                LastModifiedUtc: File.GetLastWriteTimeUtc(filePath),
                SectionPathSegments: sectionPathSegments,
                IsSectionIntro: isReadme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build documentation article for {FilePath}", filePath);
            return null;
        }
    }

    private static (string? Title, HeadingBlock? Block) ExtractTitleAndHeading(MarkdownDocument document)
    {
        var heading = document.OfType<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        if (heading is null)
        {
            return (null, null);
        }

        var text = new StringBuilder();
        if (heading.Inline is not null)
        {
            foreach (var inline in heading.Inline)
            {
                AppendInlineText(inline, text);
            }
        }

        return (text.ToString().Trim(), heading);
    }

    private static void AppendInlineText(Inline inline, StringBuilder builder)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    AppendInlineText(child, builder);
                }
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
        }
    }

    private static string? ExtractExcerpt(MarkdownDocument document)
    {
        var paragraph = document.OfType<ParagraphBlock>().FirstOrDefault();
        if (paragraph?.Inline is null)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var inline in paragraph.Inline)
        {
            AppendInlineText(inline, text);
        }

        var result = text.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private void RewriteInternalLinks(MarkdownDocument document, string sourceFilePath)
    {
        // Snapshot first because we may mutate the AST when replacing inert links with code spans.
        var links = EnumerateInlines(document).OfType<LinkInline>().ToList();
        foreach (var link in links)
        {
            if (string.IsNullOrEmpty(link.Url))
            {
                continue;
            }

            var url = link.Url!;

            // Leave absolute URLs, anchors, and mail links alone.
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("//", StringComparison.Ordinal)
                || url.StartsWith("#", StringComparison.Ordinal)
                || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rewrite = ClassifyRelativeLink(url, sourceFilePath);
            switch (rewrite.Kind)
            {
                case LinkRewriteKind.Internal:
                case LinkRewriteKind.External:
                    link.Url = rewrite.Url;
                    break;
                case LinkRewriteKind.Inert:
                    MakeLinkInert(link);
                    break;
            }
        }
    }

    private LinkRewrite ClassifyRelativeLink(string url, string sourceFilePath)
    {
        // Split off optional fragment / query for preservation.
        var fragmentIndex = url.IndexOf('#');
        var queryIndex = url.IndexOf('?');
        var splitAt = (fragmentIndex, queryIndex) switch
        {
            (-1, -1) => url.Length,
            (-1, var q) => q,
            (var f, -1) => f,
            (var f, var q) => Math.Min(f, q),
        };

        var pathPart = url[..splitAt];
        var suffix = url[splitAt..];

        if (string.IsNullOrEmpty(pathPart))
        {
            return LinkRewrite.NoChange;
        }

        var sourceDir = Path.GetDirectoryName(sourceFilePath);
        if (sourceDir is null)
        {
            return LinkRewrite.NoChange;
        }

        string absolute;
        try
        {
            absolute = Path.GetFullPath(Path.Combine(sourceDir, pathPart));
        }
        catch
        {
            return LinkRewrite.NoChange;
        }

        var docsRoot = _resolvedRoot;
        if (docsRoot is null)
        {
            return LinkRewrite.NoChange;
        }

        // 1. Inside the docs root AND under a known surfaced section (tutorials/primers) AND .md.
        //    Files like docs/BUILDING_PAGES.md exist in the docs root but aren't part of the surfaced
        //    site — they fall through to the repo-relative path below.
        if (pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && absolute.StartsWith(docsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var relative = absolute[docsRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative[..^3]; // strip ".md"
            var segments = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 0 && string.Equals(segments[^1], "README", StringComparison.OrdinalIgnoreCase))
            {
                segments = segments[..^1];
            }

            if (segments.Length > 0
                && TopLevelSections.Any(s => string.Equals(s, segments[0], StringComparison.OrdinalIgnoreCase)))
            {
                return LinkRewrite.Internal($"{DocBaseToken}/{string.Join('/', segments)}{suffix}");
            }
        }

        // 2. Outside docs root (or inside docs root but not surfaced). Treat as a repo-relative file.
        //    Use the doc root's parent as the synthetic repo root so this works identically in dev
        //    (repo) and publish (App_Docs's parent dir) — only path arithmetic, not file existence.
        var repoRoot = Path.GetDirectoryName(docsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (repoRoot is not null
            && absolute.StartsWith(repoRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_repositoryUrl))
            {
                return LinkRewrite.Inert;
            }

            var repoRelative = absolute[(repoRoot.Length + 1)..]
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return LinkRewrite.External($"{_repositoryUrl}/{repoRelative}{suffix}");
        }

        // 3. Truly external (escapes repo root with too many ..): leave alone.
        return LinkRewrite.NoChange;
    }

    private static void MakeLinkInert(LinkInline link)
    {
        // Replace the LinkInline with a CodeInline holding its visible text. This makes broken
        // references obvious as "repo file" callouts without producing a clickable but 404ing link.
        // copyChildren: false drops the link's original children (e.g. a CodeInline inside the link
        // text would otherwise survive as a sibling of the replacement, producing two <code> tags).
        var text = new StringBuilder();
        foreach (var child in link)
        {
            AppendInlineText(child, text);
        }

        var replacement = new CodeInline(text.ToString());
        link.ReplaceBy(replacement, copyChildren: false);
    }

    private enum LinkRewriteKind { NoChange, Internal, External, Inert }

    private readonly record struct LinkRewrite(LinkRewriteKind Kind, string? Url)
    {
        public static LinkRewrite NoChange => new(LinkRewriteKind.NoChange, null);
        public static LinkRewrite Inert => new(LinkRewriteKind.Inert, null);
        public static LinkRewrite Internal(string url) => new(LinkRewriteKind.Internal, url);
        public static LinkRewrite External(string url) => new(LinkRewriteKind.External, url);
    }

    private static IEnumerable<Inline> EnumerateInlines(MarkdownObject root)
    {
        foreach (var descendant in root.Descendants())
        {
            if (descendant is LeafBlock leaf && leaf.Inline is not null)
            {
                foreach (var inline in EnumerateInlinesRecursive(leaf.Inline))
                {
                    yield return inline;
                }
            }
        }
    }

    private static IEnumerable<Inline> EnumerateInlinesRecursive(ContainerInline container)
    {
        foreach (var child in container)
        {
            yield return child;
            if (child is ContainerInline c)
            {
                foreach (var grand in EnumerateInlinesRecursive(c))
                {
                    yield return grand;
                }
            }
        }
    }

    private string RenderToHtml(MarkdownDocument document)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    private string? ResolveDocsRoot()
    {
        // Order: explicit config override → published App_Docs → dev-time repo path.
        var configured = _configuration["Documentation:RootPath"];
        var candidates = new List<string?>
        {
            string.IsNullOrWhiteSpace(configured) ? null : ResolveCandidate(configured),
            ResolveCandidate("App_Docs"),
            ResolveCandidate(Path.Combine("..", "..", "docs")),
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private string ResolveCandidate(string pathOrRelative)
    {
        return Path.IsPathRooted(pathOrRelative)
            ? pathOrRelative
            : Path.Combine(_hostEnvironment.ContentRootPath, pathOrRelative);
    }

    private void EnsureWatcher(string root)
    {
        if (_watcher is not null)
        {
            return;
        }

        lock (_swapLock)
        {
            if (_watcher is not null)
            {
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(root, "*.md")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true,
                };
                _watcher.Changed += OnDocsChanged;
                _watcher.Created += OnDocsChanged;
                _watcher.Deleted += OnDocsChanged;
                _watcher.Renamed += OnDocsChanged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DocumentationService: failed to attach FileSystemWatcher on {Root}.", root);
            }
        }
    }

    private void OnDocsChanged(object sender, FileSystemEventArgs e)
    {
        lock (_swapLock)
        {
            _index = new Lazy<DocumentationIndex>(BuildIndex, isThreadSafe: true);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private static string Humanise(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        var withSpaces = Regex.Replace(slug.Replace('-', ' ').Replace('_', ' '), @"\s+", " ").Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(withSpaces.ToLower(CultureInfo.InvariantCulture));
    }
}
