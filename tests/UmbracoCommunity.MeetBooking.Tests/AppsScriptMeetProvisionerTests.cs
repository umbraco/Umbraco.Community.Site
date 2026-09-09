using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.MeetBooking.Google;
using UmbracoCommunity.MeetBooking.Models;
using Xunit;

namespace UmbracoCommunity.MeetBooking.Tests;

public class AppsScriptMeetProvisionerTests
{
    private static readonly string OkBody = """
        {"ok":true,"state":{"eventId":"evt1","htmlLink":"https://calendar.google.com/x","meetingCode":"abc-defg-hij",
        "meetUri":"https://meet.google.com/abc-defg-hij","spaceName":"spaces/XYZ","configured":true,"cohostManual":true,
        "cohostInstructions":"Open the event → gear → Co-hosts"}}
        """;

    private static readonly string FailedBody = """
        {"ok":false,"step":"cohost","error":"POST v2beta/spaces/XYZ/members → HTTP 403: preview","state":{"eventId":"evt1",
        "meetingCode":"abc-defg-hij","meetUri":"https://meet.google.com/abc-defg-hij","spaceName":"spaces/XYZ","configured":true}}
        """;

    private static (AppsScriptMeetProvisioner sut, StubHandler handler) Create(HttpStatusCode status, string body, bool configured = true)
    {
        var handler = new StubHandler(status, body);
        var client = new AppsScriptHttpClient(new HttpClient(handler));
        var options = TestHelpers.Options(o =>
        {
            if (!configured) return;
            o.AppsScript.WebAppUrl = "https://script.google.com/macros/s/ID/exec";
            o.AppsScript.SharedSecret = "s3cret";
        });
        return (new AppsScriptMeetProvisioner(client, options, NullLogger<AppsScriptMeetProvisioner>.Instance), handler);
    }

    [Fact]
    public async Task Success_body_yields_ok_with_full_state()
    {
        var (sut, _) = Create(HttpStatusCode.OK, OkBody);

        var result = await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);

        result.Ok.Should().BeTrue();
        result.State.MeetUri.Should().Be("https://meet.google.com/abc-defg-hij");
        result.State.SpaceName.Should().Be("spaces/XYZ");
        result.State.CohostManual.Should().BeTrue();
        result.State.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Failure_body_yields_step_error_and_partial_state()
    {
        var (sut, _) = Create(HttpStatusCode.OK, FailedBody);

        var result = await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Step.Should().Be("cohost");
        result.Error.Should().Contain("403");
        result.State.EventId.Should().Be("evt1");
        result.State.Configured.Should().BeTrue();
        result.State.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Request_body_carries_the_contract_fields_and_previous_state()
    {
        var (sut, handler) = Create(HttpStatusCode.OK, OkBody);
        var previous = new MeetProvisioningState { EventId = "evt1", MeetingCode = "abc-defg-hij" };

        await sut.ProvisionAsync(TestHelpers.Request(), previous, CancellationToken.None);

        var sent = JsonDocument.Parse(handler.LastBody!).RootElement;
        sent.GetProperty("token").GetString().Should().Be("s3cret");
        sent.GetProperty("recordId").GetString().Should().Be(TestHelpers.RecordId.ToString("N"));
        sent.GetProperty("title").GetString().Should().Be("Umbraco London meetup");
        sent.GetProperty("cohostEmail").GetString().Should().Be("jane@gmail.com");
        sent.GetProperty("startLocal").GetString().Should().Be("2026-09-17T19:00:00");
        sent.GetProperty("durationMinutes").GetInt32().Should().Be(90);
        sent.GetProperty("timeZone").GetString().Should().Be("Europe/London");
        sent.GetProperty("recordMeeting").GetBoolean().Should().BeTrue();
        sent.GetProperty("description").GetString().Should().Be("Monthly meetup.");
        sent.GetProperty("state").GetProperty("eventId").GetString().Should().Be("evt1");
        sent.GetProperty("state").TryGetProperty("spaceName", out _).Should().BeFalse("nulls are omitted");
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://script.google.com/macros/s/ID/exec");
    }

    [Fact]
    public async Task No_previous_state_omits_the_state_property()
    {
        var (sut, handler) = Create(HttpStatusCode.OK, OkBody);
        await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);
        JsonDocument.Parse(handler.LastBody!).RootElement.TryGetProperty("state", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Ok_with_incomplete_state_is_a_contract_failure_not_a_success()
    {
        var (sut, _) = Create(HttpStatusCode.OK, "{\"ok\":true}");

        var result = await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Step.Should().Be("contract");
    }

    [Theory]
    [InlineData("http://script.google.com/macros/s/ID/exec")]
    [InlineData("not a url")]
    public async Task Non_https_or_malformed_url_fails_without_sending(string url)
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);
        var sut = new AppsScriptMeetProvisioner(new AppsScriptHttpClient(new HttpClient(handler)),
            TestHelpers.Options(o => { o.AppsScript.WebAppUrl = url; o.AppsScript.SharedSecret = "s"; }),
            NullLogger<AppsScriptMeetProvisioner>.Instance);

        var result = await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("https");
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task Html_response_is_a_transport_failure_that_keeps_previous_state()
    {
        var (sut, _) = Create(HttpStatusCode.OK, "<!doctype html><html><body>Sign in – Google Accounts</body></html>");
        var previous = new MeetProvisioningState { EventId = "evt1" };

        var result = await sut.ProvisionAsync(TestHelpers.Request(), previous, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Step.Should().Be(AppsScriptMeetProvisioner.TransportStep);
        result.Error.Should().Contain("non-JSON").And.Contain("Sign in");
        result.State.EventId.Should().Be("evt1");
    }

    [Fact]
    public async Task Network_error_is_a_transport_failure()
    {
        var handler = new StubHandler(new HttpRequestException("boom"));
        var sut = new AppsScriptMeetProvisioner(new AppsScriptHttpClient(new HttpClient(handler)),
            TestHelpers.Options(o => { o.AppsScript.WebAppUrl = "https://x/exec"; o.AppsScript.SharedSecret = "s"; }),
            NullLogger<AppsScriptMeetProvisioner>.Instance);

        var result = await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Step.Should().Be(AppsScriptMeetProvisioner.TransportStep);
        result.Error.Should().Be("boom");
    }

    [Fact]
    public async Task Missing_configuration_fails_without_calling_anything()
    {
        var (sut, handler) = Create(HttpStatusCode.OK, OkBody, configured: false);

        var result = await sut.ProvisionAsync(TestHelpers.Request(), null, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("not configured");
        handler.LastRequest.Should().BeNull();
    }

    internal sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _throw;

        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        public StubHandler(Exception toThrow) { _throw = toThrow; _body = ""; }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (_throw is not null) throw _throw;
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
