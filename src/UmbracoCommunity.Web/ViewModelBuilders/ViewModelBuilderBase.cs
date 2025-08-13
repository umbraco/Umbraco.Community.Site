using System.Collections.Specialized;
using System.Web;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    internal abstract class ViewModelBuilderBase
    {
        protected static string? GetOptionalColor(string? color) =>
            !string.IsNullOrEmpty(color) ? CreateHtmlColor(color) : null;

        protected static string GetRequiredColor(string? color, string defaultColor) =>
            !string.IsNullOrEmpty(color) && color != "#" ? CreateHtmlColor(color) : defaultColor;

        private static string CreateHtmlColor(string color) => color.StartsWith('#') ? color : "#" + color;

        private static readonly string[] s_noWebpConversionTypes = ["webp"];
        private static readonly string[] s_vectorGraphicTypes = ["svg"];

        protected static string? GetImageUrl(
            MediaWithCrops? mediaWithCrops,
            string cropAlias,
            IImageUrlGenerator imageUrlGenerator,
            IPublishedValueFallback publishedValueFallback,
            IPublishedUrlProvider publishedUrlProvider,
            UrlMode mode = UrlMode.Relative,
            bool? localCropsOnly = false,
            int? height = null,
            int? width = null,
            bool? webp = true)
        {
            if (mediaWithCrops is null)
            {
                return null;
            }

            //if (IsVectorImage(mediaWithCrops))
            //{
            //    return publishedUrlProvider.GetMediaUrl(mediaWithCrops);
            //}

            string? cropUrl;

            if (width is not null)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    width: width,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad);
            }
            else if (height is not null)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    height: height,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad);
            }
            else if (localCropsOnly is false || mediaWithCrops.LocalCrops.HasCrops() is false)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    cropAlias,
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    mode);
            }
            else
            {
                cropUrl = mediaWithCrops.LocalCrops.GetCropUrl(cropAlias, imageUrlGenerator);
            }

            return "";// CanConvertToWebp(cropUrl, mediaWithCrops, webp) ? AppendWebpFormatter(cropUrl) : cropUrl;
        }

        //private static bool IsVectorImage(MediaWithCrops mediaWithCrops) =>
        //    mediaWithCrops.Content is UmbracoMediaVectorGraphics ||
        //    s_vectorGraphicTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension);

        //private static bool CanConvertToWebp([NotNullWhen(true)] string? cropUrl, MediaWithCrops mediaWithCrops, bool? webp = true) =>
        //    cropUrl is not null &&
        //    (webp ?? true) &&
        //    mediaWithCrops.Content is not UmbracoMediaVectorGraphics &&
        //    s_noWebpConversionTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension) == false;

        private static string AppendWebpFormatter(string cropUrl)
        {
            // cropUrl is usually, but not always, relative, UriBuilder requires absolute.
            bool isRelative = cropUrl.StartsWith('/');
            UriBuilder cropUrlBuilder = new(isRelative ? $"http://example.com{cropUrl}" : cropUrl);
            NameValueCollection query = HttpUtility.ParseQueryString(cropUrlBuilder.Query);
            query["format"] = "webp";
            cropUrlBuilder.Query = query.ToString();

            return isRelative ? cropUrlBuilder.Path + cropUrlBuilder.Query : cropUrlBuilder.Uri.ToString();
        }

        //protected static Organization? GetOrganizationSchema(
        //    IPublishedContent currentPage,
        //    IBlockCollectionBuilder builder,
        //    IImageUrlGenerator imageUrlGenerator,
        //    IPublishedValueFallback publishedValueFallback,
        //    IPublishedUrlProvider publishedUrlProvider)
        //{
        //    HomePage? contentModel = currentPage.AncestorOrSelf<HomePage>();

        //    if (contentModel == null)
        //    {
        //        return null;
        //    }

        //    Uri urlUri = Uri.TryCreate(contentModel.OrgSchemaUrl, UriKind.Absolute, out Uri? rawUrlUri) ? rawUrlUri : new Uri("https://umbraco.com");
        //    OneOrMany<Uri> sameAsUris = contentModel.OrgSchemaSocialLinks != null ? new OneOrMany<Uri>(contentModel.OrgSchemaSocialLinks.Select(link => new Uri(link))) : default;

        //    var schema = new Organization
        //    {
        //        Name = string.IsNullOrEmpty(contentModel.OrgSchemaName) ? "Umbraco" : contentModel.OrgSchemaName,
        //        Url = urlUri,
        //        SameAs = sameAsUris
        //    };

        //    var logoUrl = GetImageUrl(contentModel?.OrgSchemaLogo, string.Empty, imageUrlGenerator, publishedValueFallback, publishedUrlProvider, webp: false);

        //    Uri? logoUri = Uri.TryCreate(logoUrl, UriKind.Absolute, out Uri? rawLogoUri) ? rawLogoUri : null;
        //    if (logoUri != null)
        //    {
        //        schema.Logo = logoUri;
        //    }

        //    SchemaContactPointBlockViewModel? contactPointBlockVM = contentModel?.OrgSchemaContactPoint != null && contentModel.OrgSchemaContactPoint.Any()
        //        ? builder.Build(contentModel.OrgSchemaContactPoint)?[0] as SchemaContactPointBlockViewModel
        //        : default;

        //    if (contactPointBlockVM != null)
        //    {
        //        schema.ContactPoint = new ContactPoint
        //        {
        //            Email = contactPointBlockVM.Email,
        //            ContactType = contactPointBlockVM.Type,
        //            Telephone = contactPointBlockVM.Telephone,
        //            AvailableLanguage = new Values<Schema.NET.ILanguage, string>(contactPointBlockVM.AvailableLanguages),
        //        };
        //    }

        //    IReadOnlyList<BlockViewModelBase>? addressBlockVMs = contentModel?.OrgSchemaAddress != null && contentModel.OrgSchemaAddress.Any()
        //        ? builder.Build(contentModel.OrgSchemaAddress)
        //        : default;

        //    if (addressBlockVMs != null)
        //    {
        //        schema.Address = new Values<IPostalAddress, string>(
        //            addressBlockVMs
        //                .WhereNotNull()
        //                .Cast<SchemaAddressBlockViewModel>()
        //                .OrderByDescending(n => n.IsMain)
        //                .Select(n => new PostalAddress
        //                {
        //                    AddressCountry = n.Country,
        //                    AddressLocality = n.Locality,
        //                    StreetAddress = n.Street,
        //                    PostalCode = n.PostalCode,
        //                }));
        //    }

        //    return schema;
        //}

        //public static string? GetWebPageName(ISEO contentModel)
        //{
        //    if (contentModel is HomePage)
        //    {
        //        return contentModel.MetaTitle;
        //    }

        //    if (contentModel is BlogPost && !string.IsNullOrEmpty(contentModel.MetaTitle))
        //    {
        //        return contentModel.MetaTitle;
        //    }

        //    return (contentModel as IPublishedContent)?.Name;
        //}
    }
}
