using Examine;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Infrastructure.Examine;
using UmbracoCommunity.Web.Routing;
using UmbracoCommunity.Web.Services.Documentation.Search;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Web.Services.Documentation;

public class RegisterDocumentation : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IDocumentationService, DocumentationService>();
        builder.Services.AddScoped<DocumentationPageViewModelBuilder>();
        builder.Services.AddHttpContextAccessor();

        // Append so built-in content finders match first (e.g. /documentation -> Documentation node directly).
        // Ours only fires for the deeper URLs (/documentation/tutorials/...) that have no corresponding content node.
        builder.ContentFinders().Append<DocumentationContentFinder>();

        // Examine search index for documentation articles.
        builder.Services.AddExamineLuceneIndex<DocumentationLuceneIndex, ConfigurationEnabledDirectoryFactory>(
            DocumentationLuceneIndex.IndexName);
        builder.Services.ConfigureOptions<ConfigureDocumentationIndexOptions>();

        builder.Services.AddSingleton<IDocumentationIndexer, DocumentationIndexer>();
        builder.Services.AddSingleton<IDocumentationSearchService, DocumentationSearchService>();
        builder.Services.AddHostedService<DocumentationIndexerStartupHostedService>();
    }
}
