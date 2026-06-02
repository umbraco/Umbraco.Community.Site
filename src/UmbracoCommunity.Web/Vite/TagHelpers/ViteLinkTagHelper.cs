using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using UmbracoCommunity.Web.Vite.Models;

namespace UmbracoCommunity.Web.Vite.TagHelpers;

/// <summary>
/// Hat tip => https://github.com/umbraco/Umbraco.Demo.Cloud/blob/master/Umbraco.Demo.Cloud.Core/Vite/TagHelpers/ViteLinkTagHelper.cs
/// </summary>
[HtmlTargetElement("link", Attributes = ViteHrefAttributeName)]
public class ViteLinkTagHelper : ViteTagHelperBase
{
    [HtmlAttributeName("vite-href")]
    public string? Href { get; set; }

    public ViteLinkTagHelper(IUrlHelperFactory urlHelperFactory, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        : base(urlHelperFactory, webHostEnvironment, configuration)
    { }

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // <link rel="stylesheet" vite-href="index"></link>

        if (IsDevelopmentEnvironment() || string.IsNullOrEmpty(Href))
        {
            // Stylesheet is loaded by JavaScript chunk during development
            output.SuppressOutput();
            return;
        }

        if (UrlHelperFactory.GetUrlHelper(ViewContext) is IUrlHelper urlHelper &&
            TryGetEntryName(Href, urlHelper, out string? entryName) &&
            await GetViteManifestAsync(urlHelper) is ViteManifest viteManifest &&
            viteManifest.TryGetValue(entryName, out ViteManifestEntry? viteManifestEntry) &&
            viteManifestEntry.Css?.Length > 0)
        {
            output.Attributes.SetAttribute("href", EntryNameWithBase(viteManifestEntry.Css[0]));

            // Add additional link tags
            foreach (string css in viteManifestEntry.Css[1..])
            {
                TagBuilder linkTag = new("link");
                foreach (TagHelperAttribute attribute in output.Attributes)
                {
                    linkTag.Attributes[attribute.Name] = attribute.Value?.ToString();
                }

                linkTag.Attributes["href"] = EntryNameWithBase(css);
                output.PostElement.AppendHtml(linkTag);
            }

            return;
        }

        output.SuppressOutput();
    }
}
