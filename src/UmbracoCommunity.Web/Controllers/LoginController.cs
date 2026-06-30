using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Members;
using UmbracoCommunity.Web.Services;

namespace UmbracoCommunity.Web.Controllers;

[Route("/login")]
public class LoginController : Controller
{
    private readonly ContentContextService _contentContextService;

    public LoginController(ContentContextService contentContextService)
        => _contentContextService = contentContextService;

    [HttpGet]
    public IActionResult Index(string? returnUrl = null)
    {
        if (!IsSignInEnabled())
            return NotFound();

        ViewBag.ReturnUrl = returnUrl;
        ViewBag.Error = Request.Query["error"].ToString();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Challenge(string? returnUrl = null)
    {
        if (!IsSignInEnabled())
            return NotFound();

        var redirectUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return new ChallengeResult(GitHubExternalLoginProviderOptions.SchemeName, properties);
    }

    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }

    private bool IsSignInEnabled()
    {
        var currentPage = _contentContextService.CurrentPage;
        if (currentPage == null) return false;
        return currentPage.GetSiteSettings()?.EnableMemberSignIn ?? false;
    }
}
