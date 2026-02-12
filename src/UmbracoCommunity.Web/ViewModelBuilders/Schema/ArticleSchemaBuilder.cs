using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Utilities;
using SchemaNet = Schema.NET;

namespace UmbracoCommunity.Web.ViewModelBuilders.Schema;

/// <summary>
/// Builds Article schema for blog article pages.
/// </summary>
internal class ArticleSchemaBuilder
{
    private readonly UrlUtilities _urlUtilities;
    private readonly OrganizationSchemaBuilder _organizationSchemaBuilder;

    public ArticleSchemaBuilder(
        UrlUtilities urlUtilities,
        OrganizationSchemaBuilder organizationSchemaBuilder)
    {
        _urlUtilities = urlUtilities;
        _organizationSchemaBuilder = organizationSchemaBuilder;
    }

    /// <summary>
    /// Builds an Article schema from the article content.
    /// Returns null if the content is not an Article.
    /// </summary>
    public SchemaNet.Article? Build(IPublishedContent content, SocialSettings? socialSettings)
    {
        if (content is not Article articleContent)
        {
            return null;
        }

        var article = new SchemaNet.Article
        {
            Headline = articleContent.Name,
            DatePublished = articleContent.PublishDate != default
                ? articleContent.PublishDate
                : articleContent.CreateDate,
            DateModified = articleContent.UpdateDate
        };

        // Add article URL
        var articleUrl = _urlUtilities.GetAbsoluteUrl(articleContent);
        if (!string.IsNullOrEmpty(articleUrl) && Uri.TryCreate(articleUrl, UriKind.Absolute, out var uri))
        {
            article.Url = uri;
        }

        // Add description from meta description or teaser
        var description = articleContent.MetaDescription;
        if (string.IsNullOrEmpty(description) && articleContent.Teaser is not null)
        {
            description = articleContent.Teaser.ToString();
        }
        if (!string.IsNullOrEmpty(description))
        {
            article.Description = description;
        }

        // Add author
        if (articleContent.Author is not null)
        {
            article.Author = new SchemaNet.Person
            {
                Name = articleContent.Author.Name
            };
        }

        // Add thumbnail image
        if (articleContent.ThumbnailImage is not null)
        {
            var imageUrl = articleContent.ThumbnailImage.MediaUrl();
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var absoluteImageUrl = _urlUtilities.MakeAbsoluteUrl(imageUrl);
                if (!string.IsNullOrEmpty(absoluteImageUrl) && Uri.TryCreate(absoluteImageUrl, UriKind.Absolute, out var imageUri))
                {
                    article.Image = imageUri;
                }
            }
        }

        // Add publisher (organization)
        article.Publisher = _organizationSchemaBuilder.Build(socialSettings);

        return article;
    }
}
