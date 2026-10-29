using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class MemberApiAntiforgeryTests
{
    [Theory]
    [InlineData(typeof(ProfileApiController))]
    [InlineData(typeof(MemberFeedsApiController))]
    public void Member_api_controllers_validate_antiforgery_tokens(Type controllerType)
    {
        controllerType.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>()
            .Should().NotBeNull();
    }

    [Theory]
    [InlineData(typeof(ProfileApiController))]
    [InlineData(typeof(MemberFeedsApiController))]
    public void No_member_api_action_opts_out_of_antiforgery_validation(Type controllerType)
    {
        controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() != null)
            .Should().BeEmpty();
    }
}
