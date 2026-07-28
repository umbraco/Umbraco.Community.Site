using Examine;
using Examine.Lucene;
using Examine.Lucene.Directories;
using Examine.Lucene.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Services.Documentation.Search;

/// <summary>
/// Minimal LuceneIndex subclass — Examine's typed-registration overload (AddExamineLuceneIndex&lt;TIndex, TDirFactory&gt;)
/// requires a concrete index type. We don't need any custom logic beyond the base behaviour.
/// </summary>
public sealed class DocumentationLuceneIndex : LuceneIndex
{
    public const string IndexName = "DocumentationIndex";

    public DocumentationLuceneIndex(
        ILoggerFactory loggerFactory,
        string name,
        IOptionsMonitor<LuceneDirectoryIndexOptions> indexOptions)
        : base(loggerFactory, name, indexOptions)
    {
    }
}
