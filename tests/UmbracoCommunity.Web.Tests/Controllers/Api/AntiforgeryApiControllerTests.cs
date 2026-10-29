using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Controllers.Api;
using UmbracoCommunity.Web.Models.Api;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Controllers.Api;

public class AntiforgeryApiControllerTests
{
    private static AntiforgeryApiController CreateController(bool isLoggedIn, Mock<IAntiforgery> antiforgery)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(isLoggedIn);

        return new AntiforgeryApiController(memberManager.Object, antiforgery.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [Fact]
    public void Token_returns_401_and_issues_no_token_when_no_member_is_signed_in()
    {
        var antiforgery = new Mock<IAntiforgery>();
        var controller = CreateController(isLoggedIn: false, antiforgery);

        var result = controller.Token();

        result.Should().BeOfType<UnauthorizedResult>();
        antiforgery.Verify(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public void Token_returns_the_request_token_and_header_name_for_a_signed_in_member()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "RequestVerificationToken"));
        var controller = CreateController(isLoggedIn: true, antiforgery);

        var result = controller.Token();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(new AntiforgeryTokenResponse("request-token", "RequestVerificationToken"));
    }

    [Fact]
    public void Token_is_never_cached()
    {
        var attribute = typeof(AntiforgeryApiController)
            .GetMethod(nameof(AntiforgeryApiController.Token))!
            .GetCustomAttribute<ResponseCacheAttribute>();

        attribute.Should().NotBeNull();
        attribute!.NoStore.Should().BeTrue();
        attribute.Location.Should().Be(ResponseCacheLocation.None);
    }
}
