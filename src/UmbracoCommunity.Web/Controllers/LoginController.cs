using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Security;

namespace UmbracoCommunity.Web.Controllers;

public class LoginController(IMemberSignInManager memberSignInManager) : Controller
{
    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await memberSignInManager.SignOutAsync();
        return Redirect("/");
    }
}
