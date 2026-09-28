using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using UmbracoCommunity.ActiveCampaign.Workflows;
using Xunit;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace UmbracoCommunity.ActiveCampaign.Tests;

public class SubscribeToActiveCampaignWorkflowTests
{
    private const string SyncOk = """{"contact":{"email":"jane@example.com","id":"113","cdate":"2026-09-28T10:00:00-05:00"},"fieldValues":[]}""";
    private const string ListOk = """{"contactList":{"contact":"113","list":"7","status":1,"id":"42"}}""";
    private const string TagOk = """{"contactTag":{"contact":"113","tag":"12","id":"9"}}""";

    private readonly TestHelpers.ScriptedHandler _handler = new();
    private readonly TestHelpers.CapturingLogger<SubscribeToActiveCampaignWorkflow> _logger = new();
    private readonly Form _form = TestHelpers.Form("email", "firstName", "lastName", "newsletterConsent");

    private SubscribeToActiveCampaignWorkflow Create(
        string? baseUrl = TestHelpers.BaseUrl,
        string? apiKey = TestHelpers.ApiKey,
        Action<SubscribeToActiveCampaignWorkflow>? configure = null)
    {
        // Strict: the workflow must ask for its own named client, nothing else.
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory.Setup(f => f.CreateClient(SubscribeToActiveCampaignWorkflow.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));

        var sut = new SubscribeToActiveCampaignWorkflow(factory.Object, TestHelpers.Options(baseUrl, apiKey), _logger)
        {
            ListId = "7",
            EmailFieldAlias = "email",
            FirstNameFieldAlias = "firstName",
            LastNameFieldAlias = "lastName",
            ConsentFieldAlias = "newsletterConsent",
        };
        configure?.Invoke(sut);
        return sut;
    }

    private WorkflowExecutionContext Context(Record record) => new(record, _form, FormState.Submitted);

    private Record ValidRecord(Action<Dictionary<string, object?>>? tweak = null)
    {
        var values = new Dictionary<string, object?>
        {
            ["email"] = "jane@example.com",
            ["firstName"] = "Jane",
            ["lastName"] = "Doe",
            ["newsletterConsent"] = true,
        };
        tweak?.Invoke(values);
        return TestHelpers.Record(_form, values);
    }

    [Fact]
    public void Identity_is_stable()
    {
        var sut = Create();

        sut.Id.Should().Be(new Guid("e93cf240-8336-4561-aab0-6b6ceda07775"), "a changed id orphans every form the workflow is attached to");
        sut.Name.Should().Be("Subscribe to ActiveCampaign");
    }

    [Fact]
    public async Task Success_syncs_the_contact_then_subscribes_it_to_the_list()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.Created, ListOk);

        var status = await Create().ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Should().HaveCount(2);

        var sync = _handler.Requests[0];
        sync.Method.Should().Be(HttpMethod.Post);
        sync.Uri.Should().Be(new Uri("https://example.api-us1.com/api/3/contact/sync"));
        sync.ApiToken.Should().Be(TestHelpers.ApiKey);
        sync.ContentType.Should().Be("application/json");
        var contact = JsonDocument.Parse(sync.Body!).RootElement.GetProperty("contact");
        contact.GetProperty("email").GetString().Should().Be("jane@example.com");
        contact.GetProperty("firstName").GetString().Should().Be("Jane");
        contact.GetProperty("lastName").GetString().Should().Be("Doe");

        var list = _handler.Requests[1];
        list.Method.Should().Be(HttpMethod.Post);
        list.Uri.Should().Be(new Uri("https://example.api-us1.com/api/3/contactLists"));
        list.ApiToken.Should().Be(TestHelpers.ApiKey);
        var contactList = JsonDocument.Parse(list.Body!).RootElement.GetProperty("contactList");
        contactList.GetProperty("list").GetInt64().Should().Be(7);
        contactList.GetProperty("contact").GetInt64().Should().Be(113, "the id comes from the contact/sync response");
        contactList.GetProperty("status").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("https://example.api-us1.com/api/3")]
    [InlineData("https://example.api-us1.com/api/3/")]
    [InlineData("  https://example.api-us1.com/api/3//  ")]
    public async Task Base_url_with_or_without_a_trailing_slash_keeps_the_api_3_segment(string baseUrl)
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);

        await Create(baseUrl: baseUrl).ExecuteAsync(Context(ValidRecord()));

        _handler.Requests.Select(r => r.Uri.ToString()).Should().Equal(
            "https://example.api-us1.com/api/3/contact/sync",
            "https://example.api-us1.com/api/3/contactLists");
    }

    [Fact]
    public async Task Names_are_omitted_rather_than_blanked_when_not_mapped_or_empty()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);
        var sut = Create(configure: w => w.LastNameFieldAlias = "");

        await sut.ExecuteAsync(Context(ValidRecord(v => v["firstName"] = "  ")));

        var contact = JsonDocument.Parse(_handler.Requests[0].Body!).RootElement.GetProperty("contact");
        contact.TryGetProperty("firstName", out _).Should().BeFalse();
        contact.TryGetProperty("lastName", out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_contact_id_sent_as_a_json_number_is_accepted_too()
    {
        _handler.Respond(HttpStatusCode.OK, """{"contact":{"id":113}}""").Respond(HttpStatusCode.OK, ListOk);

        var status = await Create().ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        JsonDocument.Parse(_handler.Requests[1].Body!).RootElement
            .GetProperty("contactList").GetProperty("contact").GetInt64().Should().Be(113);
    }

    [Fact]
    public async Task Missing_email_fails_without_calling_activecampaign()
    {
        var status = await Create().ExecuteAsync(Context(ValidRecord(v => v.Remove("email"))));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("email"));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("jane@localhost")]
    [InlineData("Jane <jane@example.com>")]
    [InlineData("jane @example.com")]
    public async Task Invalid_email_fails_without_calling_activecampaign(string email)
    {
        var status = await Create().ExecuteAsync(Context(ValidRecord(v => v["email"] = email)));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
        _logger.Entries.Should().NotContain(e => e.Message.Contains(email), "the address is personal data and is never logged");
    }

    [Theory]
    [InlineData(false)]
    [InlineData("False")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Consent_not_given_completes_without_calling_activecampaign(object? consent)
    {
        var status = await Create().ExecuteAsync(Context(ValidRecord(v => v["newsletterConsent"] = consent)));

        status.Should().Be(WorkflowExecutionStatus.Completed, "declining is the submitter's choice, not a failed entry");
        _handler.Requests.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message.Contains("not ticked"));
    }

    [Fact]
    public async Task An_unticked_checkbox_with_no_stored_value_counts_as_no_consent()
    {
        var status = await Create().ExecuteAsync(Context(ValidRecord(v => v.Remove("newsletterConsent"))));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData("True")]
    [InlineData("on")]
    public async Task Consent_given_subscribes(object consent)
    {
        // bool at submit time; a string ("True") on a record reloaded for a backoffice re-run.
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);

        var status = await Create().ExecuteAsync(Context(ValidRecord(v => v["newsletterConsent"] = consent)));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_consent_alias_that_is_not_on_the_form_fails_loudly()
    {
        // Otherwise a typo reads as "nobody ever consents" and the list silently never grows.
        var sut = Create(configure: w => w.ConsentFieldAlias = "newsletterConsnet");

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("newsletterConsnet"));
    }

    [Fact]
    public async Task No_consent_alias_subscribes_without_a_consent_check()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);
        var sut = Create(configure: w => w.ConsentFieldAlias = "");

        var status = await sut.ExecuteAsync(Context(ValidRecord(v => v["newsletterConsent"] = false)));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task An_unsaved_email_alias_setting_falls_back_to_email()
    {
        // Forms loads a setting that was never saved as "" — overwriting the property initialiser.
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);
        var sut = Create(configure: w => w.EmailFieldAlias = "");

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    [Fact]
    public async Task Contact_sync_failure_logs_status_and_body_and_does_not_add_to_the_list()
    {
        _handler.Respond(HttpStatusCode.UnprocessableEntity, """{"errors":[{"title":"Email address is invalid"}]}""");

        var status = await Create().ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().ContainSingle("contactLists must not be called without a contact id");
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error
            && e.Message.Contains("422") && e.Message.Contains("Email address is invalid") && e.Message.Contains("contact/sync"));
    }

    [Fact]
    public async Task Contact_lists_failure_logs_status_and_body_and_fails()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk)
            .Respond(HttpStatusCode.NotFound, """{"message":"No Result found for List with id 7"}""");

        var status = await Create().ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().HaveCount(2);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error
            && e.Message.Contains("404") && e.Message.Contains("No Result found") && e.Message.Contains("contactLists"));
    }

    [Theory]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("""{"contact":{}}""")]
    [InlineData("""{"contact":{"id":"abc"}}""")]
    [InlineData("[]")]
    public async Task An_unusable_sync_response_fails_without_adding_to_the_list(string body)
    {
        _handler.Respond(HttpStatusCode.OK, body);
        var context = Context(ValidRecord());

        var status = await Create().ExecuteAsync(context);

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().ContainSingle();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task A_timeout_fails_and_is_surfaced_on_the_context()
    {
        var timeout = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 10 seconds elapsing.");
        _handler.Throw(timeout);
        var context = Context(ValidRecord());

        var status = await Create().ExecuteAsync(context);

        status.Should().Be(WorkflowExecutionStatus.Failed);
        context.Exception.Should().BeSameAs(timeout, "Forms raises it on WorkflowExecutionFailedNotification");
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task A_network_error_fails()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Throw(new HttpRequestException("Connection refused"));

        var status = await Create().ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("Connection refused"));
    }

    [Theory]
    [InlineData("", TestHelpers.ApiKey)]
    [InlineData(TestHelpers.BaseUrl, "")]
    [InlineData("example.api-us1.com/api/3/", TestHelpers.ApiKey)]
    [InlineData("ftp://example.api-us1.com/api/3/", TestHelpers.ApiKey)]
    public async Task Missing_configuration_fails_cleanly_without_calling_out(string baseUrl, string apiKey)
    {
        var sut = Create(baseUrl, apiKey);

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("not configured"));
        sut.GetConfigurationErrors().Should().ContainSingle();
    }

    [Fact]
    public void A_configured_workflow_reports_no_configuration_errors() =>
        Create().GetConfigurationErrors().Should().BeEmpty();

    [Fact]
    public async Task The_api_key_never_appears_in_the_log_even_when_the_response_echoes_it()
    {
        // ActiveCampaign doesn't echo the token, but a proxy error page might. Success, list failure and exceptions
        // are all exercised so every log line the workflow can write is checked.
        _handler.Respond(HttpStatusCode.OK, SyncOk)
            .Respond(HttpStatusCode.Forbidden, $$"""{"message":"Invalid token {{TestHelpers.ApiKey}}"}""");
        await Create().ExecuteAsync(Context(ValidRecord()));

        _handler.Respond(HttpStatusCode.OK, "{\"contact\":{\"token\":\"" + TestHelpers.ApiKey + "\"}}");
        await Create().ExecuteAsync(Context(ValidRecord()));

        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);
        await Create().ExecuteAsync(Context(ValidRecord()));

        _handler.Throw(new HttpRequestException("boom"));
        await Create().ExecuteAsync(Context(ValidRecord()));

        _logger.Entries.Should().NotBeEmpty();
        _logger.Entries.Should().NotContain(e => e.Message.Contains(TestHelpers.ApiKey));
        _logger.Entries.Should().Contain(e => e.Message.Contains("[redacted]"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("7.5")]
    public void ValidateSettings_requires_a_positive_numeric_list_id(string listId)
    {
        var sut = Create(configure: w => w.ListId = listId);

        sut.ValidateSettings().Should().ContainSingle().Which.Message.Should().Contain("List ID");
    }

    [Theory]
    [InlineData("7")]
    [InlineData(" 12 ")]
    public void ValidateSettings_accepts_a_numeric_list_id(string listId) =>
        Create(configure: w => w.ListId = listId).ValidateSettings().Should().BeEmpty();

    [Fact]
    public async Task An_invalid_list_id_that_slipped_past_validation_fails_without_calling_out()
    {
        var status = await Create(configure: w => w.ListId = "").ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Tags_are_added_one_call_each_in_order_after_the_subscription()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk)
            .Respond(HttpStatusCode.Created, TagOk).Respond(HttpStatusCode.Created, TagOk);
        var sut = Create(configure: w => w.TagIds = "12,34");

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Select(r => r.Uri.ToString()).Should().Equal(
            "https://example.api-us1.com/api/3/contact/sync",
            "https://example.api-us1.com/api/3/contactLists",
            "https://example.api-us1.com/api/3/contactTags",
            "https://example.api-us1.com/api/3/contactTags");

        var tags = _handler.Requests.Skip(2).ToList();
        tags.Should().AllSatisfy(r =>
        {
            r.Method.Should().Be(HttpMethod.Post);
            r.ApiToken.Should().Be(TestHelpers.ApiKey);
            r.ContentType.Should().Be("application/json");
        });
        var bodies = tags.Select(r => JsonDocument.Parse(r.Body!).RootElement.GetProperty("contactTag")).ToList();
        bodies.Select(b => b.GetProperty("contact").GetInt64()).Should().Equal([113L, 113L], "the id comes from the contact/sync response");
        bodies.Select(b => b.GetProperty("tag").GetInt64()).Should().Equal(12L, 34L);
    }

    [Fact]
    public async Task Tag_ids_tolerate_whitespace_and_are_de_duplicated()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk)
            .Respond(HttpStatusCode.OK, TagOk).Respond(HttpStatusCode.OK, TagOk);
        var sut = Create(configure: w => w.TagIds = "  34 , 12,34 ,, 12 , ");

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Skip(2)
            .Select(r => JsonDocument.Parse(r.Body!).RootElement.GetProperty("contactTag").GetProperty("tag").GetInt64())
            .Should().Equal(34L, 12L);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_tag_ids_setting_makes_no_tag_calls(string tagIds)
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk);
        var sut = Create(configure: w => w.TagIds = tagIds);

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Should().HaveCount(2);
        _handler.Requests.Should().NotContain(r => r.Uri.AbsolutePath.EndsWith("contactTags"));
    }

    [Fact]
    public async Task A_failing_tag_is_logged_as_a_warning_and_the_rest_are_still_tried()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk)
            .Respond(HttpStatusCode.UnprocessableEntity, $$"""{"errors":[{"title":"Tag not found"}],"echo":"{{TestHelpers.ApiKey}}"}""")
            .Throw(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 10 seconds elapsing."))
            .Respond(HttpStatusCode.Created, TagOk);
        var sut = Create(configure: w => w.TagIds = "12, 34, 56");
        var context = Context(ValidRecord());

        var status = await sut.ExecuteAsync(context);

        status.Should().Be(WorkflowExecutionStatus.Completed, "the contact is already subscribed");
        context.Exception.Should().BeNull();
        _handler.Requests.Should().HaveCount(5, "every tag is attempted");
        _handler.Requests[4].Body.Should().Contain("56");

        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
            && e.Message.Contains("422") && e.Message.Contains("Tag not found") && e.Message.Contains("contactTags")
            && e.Message.Contains("12") && e.Message.Contains(TestHelpers.RecordId.ToString()) && e.Message.Contains("[redacted]"));
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("34") && e.Message.Contains("Timeout"));
        _logger.Entries.Should().NotContain(e => e.Message.Contains(TestHelpers.ApiKey));
    }

    [Fact]
    public async Task A_network_error_on_a_tag_still_completes()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk).Respond(HttpStatusCode.OK, ListOk)
            .Throw(new HttpRequestException("Connection refused"));
        var sut = Create(configure: w => w.TagIds = "12");

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("Connection refused"));
    }

    [Fact]
    public async Task A_contact_lists_failure_fails_and_makes_no_tag_calls()
    {
        _handler.Respond(HttpStatusCode.OK, SyncOk)
            .Respond(HttpStatusCode.NotFound, """{"message":"No Result found for List with id 7"}""");
        var sut = Create(configure: w => w.TagIds = "12, 34");

        var status = await sut.ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().HaveCount(2);
        _handler.Requests.Should().NotContain(r => r.Uri.AbsolutePath.EndsWith("contactTags"));
    }

    [Fact]
    public async Task Consent_not_given_makes_no_tag_calls()
    {
        var sut = Create(configure: w => w.TagIds = "12, 34");

        var status = await sut.ExecuteAsync(Context(ValidRecord(v => v["newsletterConsent"] = false)));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_email_makes_no_tag_calls()
    {
        var sut = Create(configure: w => w.TagIds = "12");

        var status = await sut.ExecuteAsync(Context(ValidRecord(v => v.Remove("email"))));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("abc", "'abc'")]
    [InlineData("12, 0", "'0'")]
    [InlineData("-3", "'-3'")]
    [InlineData("12, 7.5, x", "'7.5', 'x'")]
    [InlineData("12 34", "'12 34'")]
    public void ValidateSettings_rejects_invalid_tag_ids_and_names_them(string tagIds, string named)
    {
        var sut = Create(configure: w => w.TagIds = tagIds);

        sut.ValidateSettings().Should().ContainSingle().Which.Message.Should().Contain("Tag IDs").And.Contain(named);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData(" 12 , 34, 12 ,")]
    public void ValidateSettings_accepts_blank_or_numeric_tag_ids(string tagIds) =>
        Create(configure: w => w.TagIds = tagIds).ValidateSettings().Should().BeEmpty();

    [Fact]
    public async Task Invalid_tag_ids_that_slipped_past_validation_fail_without_calling_out()
    {
        var status = await Create(configure: w => w.TagIds = "12, nope").ExecuteAsync(Context(ValidRecord()));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        _handler.Requests.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("Tag IDs"));
    }
}
