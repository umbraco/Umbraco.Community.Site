using Microsoft.AspNetCore.Http;
using Schema.NET;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Extensions;
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

        public SeoMetaDataViewModelDecorator(
            IPublishedUrlProvider publishedUrlProvider,
            IImageUrlGenerator imageUrlGenerator,
            IPublishedValueFallback publishedValueFallback,
            IPublishedContentQuery publishedContentQuery,
            IHttpContextAccessor httpContextAccessor)
        {
            _publishedUrlProvider = publishedUrlProvider;
            _imageUrlGenerator = imageUrlGenerator;
            _publishedValueFallback = publishedValueFallback;
            _publishedContentQuery = publishedContentQuery;
            _httpContextAccessor = httpContextAccessor;
        }

        public void Decorate(PageViewModelBase viewModel, IPublishedContent? currentPage)
        {
            if (currentPage is not ISeo contentModel)
            {
                return;
            }

            var socialSettings = currentPage.GetSocialSettings();

            viewModel.MetaTitle = string.IsNullOrEmpty(contentModel.MetaTitle) ? viewModel.Name : contentModel.MetaTitle ?? string.Empty;
            viewModel.MetaDescription = contentModel.MetaDescription ?? string.Empty;
            viewModel.OpenGraphImageUrl = GetOpenGraphImageUrl(contentModel.OgImage, socialSettings);
            viewModel.Robots = contentModel.Robots ?? string.Empty;
            viewModel.CanonicalUrl = GetCanonicalUrl(contentModel);

            AddPageQuery(viewModel);
            AddBaseSchema(viewModel, contentModel);
        }

        private string? GetCanonicalUrl(ISeo contentModel)
        {
            string? contentRelativeUrl = contentModel is IPublishedContent content ? content.Url(_publishedUrlProvider, null, UrlMode.Relative) : null;

            if (string.IsNullOrEmpty(contentRelativeUrl))
            {
                return null;
            }

            var baseUri = new Uri($"https://community.umbraco.com"); // TODO: replace with site specific uri

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

        private static void AddBaseSchema(PageViewModelBase viewModel, ISeo contentModel)
        {
            if (!string.IsNullOrEmpty(contentModel.CustomSchema))
            {
                viewModel.AddSchemaMarkup(contentModel.CustomSchema);
            }

            WebPage? webPageSchema = GetWebPageSchema(contentModel);

            if (webPageSchema is not null)
            {
                viewModel.AddSchemaMarkup(webPageSchema.ToHtmlEscapedString());
            }
        }

        private static WebPage? GetWebPageSchema(ISeo contentModel)
        {
            var webPageName = GetWebPageName(contentModel);

            if (string.IsNullOrEmpty(webPageName))
            {
                return null;
            }

            return new WebPage
            {
                Name = new OneOrMany<string>(webPageName),
                AlternateName = "Umbraco"
            };
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
