using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Text;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;

namespace UmbracoCommunity.Web.Controllers;

public class RobotsController : Controller
{
    public const string CustomRobotsTxtCacheKeyPrefix = "robots-txt-custom::";

    private static readonly TimeSpan CustomRobotsTxtCacheTtl = TimeSpan.FromMinutes(10);

    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly AppCaches _appCaches;

    public RobotsController(
        IWebHostEnvironment webHostEnvironment,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedContentQuery publishedContentQuery,
        AppCaches appCaches)
    {
        _webHostEnvironment = webHostEnvironment;
        _umbracoContextFactory = umbracoContextFactory;
        _publishedContentQuery = publishedContentQuery;
        _appCaches = appCaches;
    }

    public IActionResult Index()
    {
        // Deploy copies Settings between environments, so never let a custom file unblock staging.
        if (IsNonProductionEnvironment())
        {
            return Content(BuildDefaultRobotsTxt(), "text/plain");
        }

        var customRobotsTxt = ResolveCustomRobotsTxt();

        return Content(
            string.IsNullOrWhiteSpace(customRobotsTxt) ? BuildDefaultRobotsTxt() : customRobotsTxt,
            "text/plain");
    }

    private bool IsNonProductionEnvironment()
        => _webHostEnvironment.IsDevelopment() || _webHostEnvironment.IsStaging();

    private string? ResolveCustomRobotsTxt()
    {
        using var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();
        var umbracoContext = umbracoContextReference.UmbracoContext;

        var tenantRoot = ResolveTenantRoot(umbracoContext);
        if (tenantRoot is null)
        {
            return null;
        }

        return _appCaches.RuntimeCache.GetCacheItem(
            $"{CustomRobotsTxtCacheKeyPrefix}{tenantRoot.Key}",
            () => tenantRoot.GetSiteSettings()?.CustomRobotsTxt ?? string.Empty,
            timeout: CustomRobotsTxtCacheTtl);
    }

    private IPublishedContent? ResolveTenantRoot(IUmbracoContext umbracoContext)
    {
        var domains = umbracoContext.Domains?.GetAll(includeWildcards: false) ?? Enumerable.Empty<Domain>();
        var domainAndUri = DomainUtilities.SelectDomain(domains, new Uri($"{Request.Scheme}://{Request.Host.Host}"));
        var domainRoot = domainAndUri is not null
            ? umbracoContext.Content?.GetById(domainAndUri.ContentId)
            : null;

        return domainRoot ?? _publishedContentQuery.ContentAtRoot().FirstOrDefault();
    }

    private string BuildDefaultRobotsTxt()
    {
        var builder = new StringBuilder();

        builder.AppendLine("User-agent: *");

        if (IsNonProductionEnvironment())
        {
            builder.AppendLine("Disallow: /");
        }
        else
        {
            builder.AppendLine($"Sitemap: {Request.Scheme}://{Request.Host}/sitemap/");
            builder.AppendLine("Disallow: /App_Plugins/");
            builder.AppendLine("Disallow: /runtimes/");
            builder.AppendLine("Disallow: /wwwroot/");
            builder.AppendLine("Disallow: /umbraco/");
            builder.AppendLine("Disallow: /views/");
        }

        builder.AppendLine();
        builder.Append(GetUmbracoTextLogo());

        return builder.ToString();
    }

    private static string GetUmbracoTextLogo()
            => @"#                                       %%%%%%%%%                                   
#                             %%%%%%%%%%%%%%%%%%%%%%%%%%%%#                         
#                       (%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%                    
#                    %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%                
#                *%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%.            
#              %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%          
#            %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%        
#          %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%      
#        (%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%     
#       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%#   
#      %%%%%%%%%%%%%%%#      %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%(      %%%%%%%%%%%%%%%#  
#     %%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%# 
#    #%%%%%%%%%%%%%%%%      (%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%% 
#    %%%%%%%%%%%%%%%%#      %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%(      (%%%%%%%%%%%%%%%%
#    %%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%      /%%%%%%%%%%%%%%%%
#   %%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%
#   %%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%
#   %%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%
#    %%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%(       %%%%%%%%%%%%%%%%
#    %%%%%%%%%%%%%%%%#       %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%       %%%%%%%%%%%%%%%%%
#    *%%%%%%%%%%%%%%%%        %%%%%%%%%%%%%%%%%%%%%%%%%%%%%        %%%%%%%%%%%%%%%% 
#     #%%%%%%%%%%%%%%%%          (%%%%%%%%%%%%%%%%%%%%%/          %%%%%%%%%%%%%%%%/ 
#      %%%%%%%%%%%%%%%%%%                                       %%%%%%%%%%%%%%%%%#  
#       #%%%%%%%%%%%%%%%%%%.                                 %%%%%%%%%%%%%%%%%%%(   
#         %%%%%%%%%%%%%%%%%%%%%%%%%%                 %%%%%%%%%%%%%%%%%%%%%%%%%%     
#          %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%(      
#            %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%        
#              %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%/          
#                 %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%             
#                    %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%                
#                        #%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%                    
#                             #%%%%%%%%%%%%%%%%%%%%%%%%%%%/                         ";
}
