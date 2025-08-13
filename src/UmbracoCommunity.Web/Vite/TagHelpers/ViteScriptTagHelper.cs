using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using UmbracoCommunity.Web.Vite.Models;

namespace UmbracoCommunity.Web.Vite.TagHelpers;

/// <summary>
/// Hat tip => https://github.com/umbraco/Umbraco.Demo.Cloud/blob/master/Umbraco.Demo.Cloud.Core/Vite/TagHelpers/ViteScriptTagHelper.cs
/// </summary>
[HtmlTargetElement("script", Attributes = ViteSrcAttributeName)]
public class ViteScriptTagHelper : ViteTagHelperBase
{
    [HtmlAttributeName("vite-src")]
    public string? Src { get; set; }

    [HtmlAttributeName("vite-client")]
    public bool? Client { get; set; }

    public ViteScriptTagHelper(IConfiguration configuration, IUrlHelperFactory urlHelperFactory, IWebHostEnvironment webHostEnvironment)
        : base(urlHelperFactory, webHostEnvironment, configuration)
    {
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // <script type="module" vite-src="index" ></script>

        IUrlHelper urlHelper = UrlHelperFactory.GetUrlHelper(ViewContext);

        if (string.IsNullOrEmpty(Src) || !TryGetEntryName(Src, urlHelper, out string? entryName))
        {
            output.SuppressOutput();
            return;
        }

        if (IsLocalEnvironment())
        {
            output.Attributes.SetAttribute("src", ViteDevServerUrl + "/" + entryName);
            output.Attributes.SetAttribute("type", "module");

            // prevent duplicates by rendering only when vite-client attribute is set to true
            if (Client is true)
            {
                TagBuilder scriptTag = new("script");
                scriptTag.Attributes["type"] = "module";
                scriptTag.Attributes["src"] = ViteDevServerUrl + "/@vite/client";
                output.PreElement.AppendHtml(scriptTag);
            }

            return;
        }

        if (await GetViteManifestAsync(urlHelper) is ViteManifest viteManifest &&
            viteManifest.TryGetValue(entryName, out ViteManifestEntry? viteManifestEntry))
        {
            output.Attributes.SetAttribute("src", EntryNameWithBase(viteManifestEntry.File));
            output.Attributes.SetAttribute("type", "module");
            return;
        }

        output.SuppressOutput();
    }
}
