using Microsoft.AspNetCore.Html;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Models.Pages.Documentation;
using UmbracoCommunity.Web.Services.Documentation;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages;

public class DocumentationPageViewModelBuilder
{
    private readonly IDocumentationService _documentationService;

    public DocumentationPageViewModelBuilder(IDocumentationService documentationService)
    {
        _documentationService = documentationService;
    }

    public DocumentationPageViewModel? Build(IPublishedContent currentPage, IUmbracoContext umbracoContext, IReadOnlyList<string> segments)
    {
        var index = _documentationService.GetIndex();
        var docRootUrl = currentPage.Url(mode: UrlMode.Relative).TrimEnd('/');
        var docRootTitle = currentPage.Name ?? "Documentation";

        var crumbs = new List<DocumentationCrumb>
        {
            new(docRootTitle, docRootUrl + "/"),
        };

        if (segments.Count == 0)
        {
            return new DocumentationPageViewModel(currentPage)
            {
                Mode = DocumentationViewMode.Root,
                DocRootUrl = docRootUrl,
                DocRootTitle = docRootTitle,
                Index = index,
                Crumbs = crumbs,
            };
        }

        var resolution = _documentationService.Resolve(segments);
        if (resolution.IsNotFound)
        {
            return null;
        }

        if (resolution.Article is { } article)
        {
            AppendCrumbsForSegments(crumbs, index, article.SectionPathSegments, docRootUrl);
            crumbs.Add(new DocumentationCrumb(article.Title, ComposeUrl(docRootUrl, article.PathSegments)));

            var resolvedHtml = ResolveDocLinks(article.HtmlContent, docRootUrl);
            return new DocumentationPageViewModel(currentPage)
            {
                Mode = DocumentationViewMode.Article,
                DocRootUrl = docRootUrl,
                DocRootTitle = docRootTitle,
                Index = index,
                Crumbs = crumbs,
                Article = article,
                ArticleHtml = new HtmlString(resolvedHtml),
            };
        }

        if (resolution.Section is { } section)
        {
            AppendCrumbsForSegments(crumbs, index, section.PathSegments, docRootUrl);

            HtmlString? introHtml = null;
            if (section.Intro is { } intro)
            {
                introHtml = new HtmlString(ResolveDocLinks(intro.HtmlContent, docRootUrl));
            }

            return new DocumentationPageViewModel(currentPage)
            {
                Mode = DocumentationViewMode.Section,
                DocRootUrl = docRootUrl,
                DocRootTitle = docRootTitle,
                Index = index,
                Crumbs = crumbs,
                Section = section,
                SectionIntroHtml = introHtml,
            };
        }

        return null;
    }

    private static void AppendCrumbsForSegments(
        List<DocumentationCrumb> crumbs,
        DocumentationIndex index,
        IReadOnlyList<string> sectionPathSegments,
        string docRootUrl)
    {
        IReadOnlyList<DocumentationSection> currentSections = index.Sections;
        for (var i = 0; i < sectionPathSegments.Count; i++)
        {
            var segment = sectionPathSegments[i];
            var match = currentSections.FirstOrDefault(s => string.Equals(s.Slug, segment, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                break;
            }

            crumbs.Add(new DocumentationCrumb(match.Title, ComposeUrl(docRootUrl, match.PathSegments)));
            currentSections = match.Subsections;
        }
    }

    public static string ComposeUrl(string docRootUrl, IReadOnlyList<string> pathSegments)
    {
        if (pathSegments.Count == 0)
        {
            return docRootUrl + "/";
        }

        return docRootUrl + "/" + string.Join('/', pathSegments) + "/";
    }

    private static string ResolveDocLinks(string html, string docRootUrl)
    {
        return html.Replace(DocumentationService.DocBaseToken, docRootUrl, StringComparison.Ordinal);
    }
}
