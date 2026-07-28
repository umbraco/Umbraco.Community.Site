using Microsoft.AspNetCore.Html;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Services.Documentation;

namespace UmbracoCommunity.Web.Models.Pages.Documentation;

public enum DocumentationViewMode
{
    Root,
    Section,
    Article,
}

public class DocumentationPageViewModel : PageViewModelBase
{
    public DocumentationPageViewModel(IPublishedContent currentPage) : base(currentPage)
    {
    }

    public DocumentationViewMode Mode { get; init; }

    /// <summary>Documentation node's URL (used to compose absolute links to sections/articles).</summary>
    public required string DocRootUrl { get; init; }

    public required string DocRootTitle { get; init; }

    public required DocumentationIndex Index { get; init; }

    /// <summary>Crumbs from the doc root through to the current section/article.</summary>
    public IReadOnlyList<DocumentationCrumb> Crumbs { get; init; } = [];

    public DocumentationSection? Section { get; init; }

    public DocumentationArticle? Article { get; init; }

    /// <summary>Rendered article HTML with internal links resolved against the tenant's Documentation node URL.</summary>
    public HtmlString? ArticleHtml { get; init; }

    /// <summary>Section intro HTML when displaying a section index that has a README.md.</summary>
    public HtmlString? SectionIntroHtml { get; init; }
}

public sealed record DocumentationCrumb(string Title, string Url);
