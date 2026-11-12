using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoCommunity.Web.Features.Profiles;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class DevRelProfileViewTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("?devrel=", false)]
    [InlineData("?devrel=public", false)]
    [InlineData("?other=all", false)]
    [InlineData("?devrel=all", true)]
    [InlineData("?devrel=ALL", true)]
    public void Everything_is_shown_only_when_the_query_string_asks_for_it(string queryString, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);

        DevRelProfileView.IsShowEverythingRequested(context.Request.Query).Should().Be(expected);
    }

    [Fact]
    public void The_toggle_links_switch_between_the_public_view_and_everything()
    {
        var path = new PathString("/p/octocat/");

        DevRelProfileView.PublicViewUrl(path).Should().Be("/p/octocat/");
        DevRelProfileView.ShowEverythingUrl(path).Should().Be("/p/octocat/?devrel=all");
    }

    [Fact]
    public void A_DevRel_view_is_not_stored_by_any_cache()
    {
        var context = new DefaultHttpContext();

        DevRelProfileView.PreventCaching(context.Response);

        context.Response.Headers.CacheControl.ToString().Should().Be("private, no-store");
    }
}
