using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Controllers.Render;

public class AccountPageController : RenderController
{
    private readonly IMemberManager _memberManager;

    public AccountPageController(
        ILogger<AccountPageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IMemberManager memberManager)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
        => _memberManager = memberManager;

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!_memberManager.IsLoggedIn())
            return Redirect("/");

        var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");
        var member = await _memberManager.GetCurrentMemberAsync();

        var handle = member?.UserName;
        var viewModel = new AccountPageViewModel(currentPage)
        {
            DisplayName = member?.Name,
            GitHubHandle = handle,
            Email = member?.Email,
            AvatarUrl = handle != null ? $"https://github.com/{handle}.png" : null,
        };

        return CurrentTemplate(viewModel);
    }
}
