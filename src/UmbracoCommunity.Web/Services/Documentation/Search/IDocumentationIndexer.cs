namespace UmbracoCommunity.Web.Services.Documentation.Search;

public interface IDocumentationIndexer
{
    /// <summary>Wipes and repopulates the Examine index from the current DocumentationService state.</summary>
    void RebuildIndex();
}
