using Microsoft.AspNetCore.Http;
using Schema.NET;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Utilities;
using UmbracoCommunity.Web.ViewModelBuilders;
using UmbracoCommunity.Web.ViewModelBuilders.Schema;

namespace UmbracoCommunity.Web.Services;

internal class SeoDataService : ViewModelBuilderBase, ISeoDataService
{
    private readonly IImageUrlBuilder _imageUrlBuilder;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SessionizeApiClient _sessionizeApiClient;
    private readonly OrganizationSchemaBuilder _organizationSchemaBuilder;
    private readonly ArticleSchemaBuilder _articleSchemaBuilder;
    private readonly BreadcrumbSchemaBuilder _breadcrumbSchemaBuilder;
    private readonly UrlUtilities _urlUtilities;

    public SeoDataService(
        IImageUrlBuilder imageUrlBuilder,
        IHttpContextAccessor httpContextAccessor,
        SessionizeApiClient sessionizeApiClient,
        OrganizationSchemaBuilder organizationSchemaBuilder,
        ArticleSchemaBuilder articleSchemaBuilder,
        BreadcrumbSchemaBuilder breadcrumbSchemaBuilder,
        UrlUtilities urlUtilities)
    {
        _imageUrlBuilder = imageUrlBuilder;
        _httpContextAccessor = httpContextAccessor;
        _sessionizeApiClient = sessionizeApiClient;
        _organizationSchemaBuilder = organizationSchemaBuilder;
        _articleSchemaBuilder = articleSchemaBuilder;
        _breadcrumbSchemaBuilder = breadcrumbSchemaBuilder;
        _urlUtilities = urlUtilities;
    }

    public async Task<MetaTagsViewModel> BuildAsync(IPublishedContent currentPage)
    {
        var viewModel = new MetaTagsViewModel
        {
            Name = currentPage.Name
        };

        var siteSettings = currentPage.GetSiteSettings();
        viewModel.SiteName = siteSettings?.SiteName;

        if (currentPage is ICompositionSeo contentModel)
        {
            viewModel.MetaTitle = string.IsNullOrEmpty(contentModel.MetaTitle) ? currentPage.Name : contentModel.MetaTitle;
            viewModel.MetaDescription = contentModel.MetaDescription ?? string.Empty;

            var socialSettings = currentPage.GetSocialSettings(siteSettings);
            viewModel.OpenGraphImageUrl = GetOpenGraphImageUrl(contentModel.OgImage, socialSettings);
            viewModel.Robots = contentModel.Robots ?? string.Empty;
            viewModel.CanonicalUrl = GetCanonicalUrl(contentModel);

            AddPageQuery(viewModel);
            AddBaseSchema(viewModel, contentModel, socialSettings, currentPage);

            await ApplySessionOpenGraphOverridesAsync(viewModel);
        }
        else
        {
            viewModel.MetaTitle = currentPage.Name;
        }

        return viewModel;
    }

    private async Task ApplySessionOpenGraphOverridesAsync(MetaTagsViewModel viewModel)
    {
        if (_httpContextAccessor.HttpContext is null)
        {
            return;
        }

        var sessionId = _httpContextAccessor.HttpContext.Request.Query["session"].ToString();
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        try
        {
            var session = await _sessionizeApiClient.GetSessionByIdAsync(sessionId);
            if (session is null)
            {
                return;
            }

            var speakers = session.Speakers?.Count > 0
                ? $" by {string.Join(", ", session.Speakers.Select(s => s.FullName))}"
                : string.Empty;
            viewModel.MetaTitle = $"{session.Title}{speakers}";

            if (!string.IsNullOrEmpty(session.Description))
            {
                var description = session.Description.Length > 200
                    ? session.Description[..197] + "..."
                    : session.Description;
                viewModel.MetaDescription = description;
            }

            if (!string.IsNullOrEmpty(viewModel.CanonicalUrl))
            {
                var separator = viewModel.CanonicalUrl.Contains('?') ? "&" : "?";
                viewModel.CanonicalUrl = $"{viewModel.CanonicalUrl}{separator}session={sessionId}";
            }
        }
        catch
        {
            // If fetching session fails, just use the default OG tags
        }
    }

    private string? GetCanonicalUrl(ICompositionSeo contentModel)
    {
        if (contentModel is not IPublishedContent content)
        {
            return null;
        }

        return _urlUtilities.GetAbsoluteUrl(content);
    }

    private string GetOpenGraphImageUrl(MediaWithCrops? mediaWithCrops, SocialSettings? socialSettings)
    {
        MediaWithCrops media;
        if (mediaWithCrops is null)
        {
            if (socialSettings?.SiteWideOgImage is null)
            {
                return string.Empty;
            }

            media = socialSettings.SiteWideOgImage;
        }
        else
        {
            media = mediaWithCrops;
        }

        return GetEnsuredOpenGraphImageUrl(media);
    }

    private string GetEnsuredOpenGraphImageUrl(MediaWithCrops mediaWithCrops) =>
        _imageUrlBuilder.GetImageUrl(
            mediaWithCrops,
            "Social",
            mode: UrlMode.Absolute,
            webp: false) ?? string.Empty;

    private void AddBaseSchema(MetaTagsViewModel viewModel, ICompositionSeo contentModel, SocialSettings? socialSettings, IPublishedContent currentPage)
    {
        if (!string.IsNullOrEmpty(contentModel.CustomSchema))
        {
            viewModel.AddSchemaMarkup(contentModel.CustomSchema);
        }

        var articleSchema = _articleSchemaBuilder.Build(currentPage, socialSettings);
        if (articleSchema is not null)
        {
            viewModel.AddSchemaMarkup(articleSchema.ToHtmlEscapedString());
        }
        else
        {
            WebPage? webPageSchema = GetWebPageSchema(viewModel, contentModel, socialSettings);
            if (webPageSchema is not null)
            {
                viewModel.AddSchemaMarkup(webPageSchema.ToHtmlEscapedString());
            }
        }

        var breadcrumbSchema = _breadcrumbSchemaBuilder.Build(currentPage);
        if (breadcrumbSchema is not null)
        {
            viewModel.AddSchemaMarkup(breadcrumbSchema.ToHtmlEscapedString());
        }
    }

    private WebPage? GetWebPageSchema(MetaTagsViewModel viewModel, ICompositionSeo contentModel, SocialSettings? socialSettings)
    {
        var webPageName = GetWebPageName(contentModel);

        if (string.IsNullOrEmpty(webPageName))
        {
            return null;
        }

        var webPage = new WebPage
        {
            Name = new OneOrMany<string>(webPageName),
        };

        if (!string.IsNullOrEmpty(viewModel.MetaDescription))
        {
            webPage.Description = viewModel.MetaDescription;
        }

        if (!string.IsNullOrEmpty(viewModel.CanonicalUrl))
        {
            webPage.Url = new OneOrMany<Uri>(new Uri(viewModel.CanonicalUrl));
        }

        webPage.Publisher = _organizationSchemaBuilder.Build(socialSettings);

        return webPage;
    }

    private void AddPageQuery(MetaTagsViewModel viewModel)
    {
        if (_httpContextAccessor.HttpContext is null)
        {
            return;
        }

        var pageParam = _httpContextAccessor.HttpContext.Request.Query["page"].ToString();

        if (string.IsNullOrEmpty(pageParam))
        {
            return;
        }

        if (int.TryParse(pageParam, out var page) is false)
        {
            return;
        }

        var nextPageInt = page + 1;

        if (page > 1)
        {
            var prevPageInt = page - 1;
            var isSecondPage = prevPageInt == 1;
            viewModel.PrevUrl = $"{viewModel.CanonicalUrl}{(isSecondPage ? string.Empty : "?page=" + prevPageInt)}";
        }

        viewModel.NextUrl = $"{viewModel.CanonicalUrl}?page={nextPageInt}";

        viewModel.CanonicalUrl += $"?page={pageParam}";
    }
}
