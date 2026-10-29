using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class GitHubProfileClientTests
{
    private static GitHubProfileClient CreateClient(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") }, NullLogger<GitHubProfileClient>.Instance);

    [Fact]
    public async Task Reads_the_bio_from_the_users_endpoint()
    {
        Uri? requested = null;
        var handler = new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "login": "octocat", "bio": "  Building things  " }"""),
            };
        });

        var result = await CreateClient(handler).GetProfileAsync("octocat");

        requested.Should().Be(new Uri("https://api.github.com/users/octocat"));
        result.Succeeded.Should().BeTrue();
        result.Bio.Should().Be("Building things");
    }

    [Theory]
    [InlineData("""{ "login": "octocat", "bio": null }""")]
    [InlineData("""{ "login": "octocat", "bio": "   " }""")]
    [InlineData("""{ "login": "octocat" }""")]
    public async Task A_missing_or_empty_bio_is_a_successful_lookup_with_no_bio(string body)
    {
        var result = await CreateClient(StubHandler.Json(body)).GetProfileAsync("octocat");

        result.Succeeded.Should().BeTrue();
        result.Bio.Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_user_is_a_successful_lookup_with_no_bio_or_company()
    {
        var result = await CreateClient(StubHandler.Status(HttpStatusCode.NotFound)).GetProfileAsync("octocat");

        result.Succeeded.Should().BeTrue();
        result.Bio.Should().BeNull();
        result.Company.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_error_response_is_a_failure(HttpStatusCode status)
    {
        var result = await CreateClient(StubHandler.Status(status)).GetProfileAsync("octocat");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task A_network_error_is_a_failure()
    {
        var result = await CreateClient(StubHandler.Throws()).GetProfileAsync("octocat");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task A_timeout_is_a_failure()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("simulated timeout"));

        var result = await CreateClient(handler).GetProfileAsync("octocat");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Malformed_json_is_a_failure()
    {
        var result = await CreateClient(StubHandler.Json("not json")).GetProfileAsync("octocat");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task A_long_bio_is_cut_to_the_bio_limit()
    {
        var longBio = new string('a', ProfileText.MaxBioLength + 50);

        var result = await CreateClient(StubHandler.Json($$"""{ "bio": "{{longBio}}" }""")).GetProfileAsync("octocat");

        result.Bio.Should().HaveLength(ProfileText.MaxBioLength);
    }

    [Theory]
    [InlineData("\"@umbraco\"", "umbraco")]
    [InlineData("\"  Umbraco HQ  \"", "Umbraco HQ")]
    [InlineData("\"@\"", null)]
    [InlineData("\"   \"", null)]
    [InlineData("null", null)]
    public async Task Reads_the_company_without_its_leading_at_sign(string companyJson, string? expected)
    {
        var result = await CreateClient(StubHandler.Json($$"""{ "bio": null, "company": {{companyJson}} }""")).GetProfileAsync("octocat");

        result.Succeeded.Should().BeTrue();
        result.Company.Should().Be(expected);
    }

    [Fact]
    public async Task A_long_company_is_cut_to_the_headline_limit()
    {
        var longCompany = new string('a', ProfileText.MaxHeadlineLength + 50);

        var result = await CreateClient(StubHandler.Json($$"""{ "company": "@{{longCompany}}" }""")).GetProfileAsync("octocat");

        result.Company.Should().Be(new string('a', ProfileText.MaxHeadlineLength));
    }

    [Fact]
    public async Task Bio_and_company_come_from_one_request()
    {
        var handler = StubHandler.Json("""{ "bio": "Building things", "company": "@umbraco" }""");

        var result = await CreateClient(handler).GetProfileAsync("octocat");

        result.Bio.Should().Be("Building things");
        result.Company.Should().Be("umbraco");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task A_blank_handle_is_not_looked_up()
    {
        var handler = StubHandler.Json("""{ "bio": "x" }""");

        var result = await CreateClient(handler).GetProfileAsync(" ");

        result.Succeeded.Should().BeFalse();
        handler.CallCount.Should().Be(0);
    }
}
