using Microsoft.AspNetCore.Http;
using Schema.NET;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    internal class SeoMetaDataViewModelDecorator : ViewModelBuilderBase, IPageViewModelDecorator<Seo>
    {
        private readonly IPublishedUrlProvider _publishedUrlProvider;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SessionizeApiClient _sessionizeApiClient;
        private readonly OrganizationSchemaBuilder _organizationSchemaBuilder;

        public SeoMetaDataViewModelDecorator(
            IPublishedUrlProvider publishedUrlProvider,
            IImageUrlGenerator imageUrlGenerator,
            IPublishedValueFallback publishedValueFallback,
            IPublishedContentQuery publishedContentQuery,
            IHttpContextAccessor httpContextAccessor,
            SessionizeApiClient sessionizeApiClient,
            OrganizationSchemaBuilder organizationSchemaBuilder)
        {
            _publishedUrlProvider = publishedUrlProvider;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedValueFallback = publishedValueFallback;
            _publishedContentQuery = publishedContentQuery;
            _httpContextAccessor = httpContextAccessor;
            _sessionizeApiClient = sessionizeApiClient;
            _organizationSchemaBuilder = organizationSchemaBuilder;
        }

        public async Task DecorateAsync(PageViewModelBase viewModel, IPublishedContent? currentPage)
        {
            if (currentPage is not ISeo contentModel)
            {
                return;
            }

            var siteSettings = currentPage.GetSiteSettings();
            viewModel.SiteName = siteSettings?.SiteName;
            viewModel.Favicon = siteSettings?.Favicon;

            viewModel.MetaTitle = string.IsNullOrEmpty(contentModel.MetaTitle) ? viewModel.Name : contentModel.MetaTitle;
            viewModel.MetaDescription = contentModel.MetaDescription ?? string.Empty;

            var socialSettings = currentPage.GetSocialSettings(siteSettings);
            viewModel.OpenGraphImageUrl = GetOpenGraphImageUrl(contentModel.OgImage, socialSettings);

            viewModel.Robots = contentModel.Robots ?? string.Empty;
            viewModel.CanonicalUrl = GetCanonicalUrl(contentModel);

            AddPageQuery(viewModel);
            AddBaseSchema(viewModel, contentModel, socialSettings);

            // Override OG tags if a session parameter is present (for social sharing)
            await ApplySessionOpenGraphOverridesAsync(viewModel);
        }

        /// <summary>
        /// Checks for a ?session= query parameter and overrides OG tags with session-specific data
        /// for better social media previews when sharing session links.
        /// </summary>
        private async Task ApplySessionOpenGraphOverridesAsync(PageViewModelBase viewModel)
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

                // Build session-specific title
                var speakers = session.Speakers?.Count > 0
                    ? $" by {string.Join(", ", session.Speakers.Select(s => s.FullName))}"
                    : string.Empty;
                viewModel.MetaTitle = $"{session.Title}{speakers}";

                // Use session description for meta description (truncate if too long)
                if (!string.IsNullOrEmpty(session.Description))
                {
                    var description = session.Description.Length > 200
                        ? session.Description[..197] + "..."
                        : session.Description;
                    viewModel.MetaDescription = description;
                }

                // Update canonical URL to include session parameter
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

        private string? GetCanonicalUrl(ISeo contentModel)
        {
            string? contentRelativeUrl = contentModel is IPublishedContent content ? content.Url(_publishedUrlProvider, null, UrlMode.Relative) : null;

            if (string.IsNullOrEmpty(contentRelativeUrl))
            {
                return null;
            }

            var baseUri = new Uri("https://community.umbraco.com");
            if (_httpContextAccessor.HttpContext is not null)
            {
                var hostname = _httpContextAccessor.HttpContext.Request.Host.Host;
                if (!string.IsNullOrEmpty(hostname))
                {
                    baseUri = new Uri($"{_httpContextAccessor.HttpContext.Request.Scheme}://{hostname}");
                }
            }

            return new Uri(baseUri, contentRelativeUrl).ToString();
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
            GetImageUrl(
                mediaWithCrops,
                "Social",
                _imageUrlGenerator,
                _publishedValueFallback,
                _publishedUrlProvider,
                UrlMode.Absolute,
                webp: false) ?? string.Empty;

        private void AddBaseSchema(PageViewModelBase viewModel, ISeo contentModel, SocialSettings? socialSettings)
        {
            if (!string.IsNullOrEmpty(contentModel.CustomSchema))
            {
                viewModel.AddSchemaMarkup(contentModel.CustomSchema);
            }

            WebPage? webPageSchema = GetWebPageSchema(viewModel, contentModel, socialSettings);

            if (webPageSchema is not null)
            {
                viewModel.AddSchemaMarkup(webPageSchema.ToHtmlEscapedString());
            }
        }

        private WebPage? GetWebPageSchema(PageViewModelBase viewModel, ISeo contentModel, SocialSettings? socialSettings)
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

            // Add description if available
            if (!string.IsNullOrEmpty(viewModel.MetaDescription))
            {
                webPage.Description = viewModel.MetaDescription;
            }

            // Add URL if canonical is set
            if (!string.IsNullOrEmpty(viewModel.CanonicalUrl))
            {
                webPage.Url = new OneOrMany<Uri>(new Uri(viewModel.CanonicalUrl));
            }

            // Add Organization as publisher (uses site settings or Umbraco defaults)
            webPage.Publisher = _organizationSchemaBuilder.Build(socialSettings);

            return webPage;
        }

        /// <summary>
        /// Adding the page parameter to canonical, prev and next url for pages with pagination.
        /// </summary>
        private void AddPageQuery(PageViewModelBase viewModel)
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

}
