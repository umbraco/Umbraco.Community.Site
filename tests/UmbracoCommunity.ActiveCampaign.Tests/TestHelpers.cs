using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;

namespace UmbracoCommunity.ActiveCampaign.Tests;

/// <summary>Hand-rolled fixtures: forms, records, options, a scripted HTTP handler and a capturing logger.</summary>
internal static class TestHelpers
{
    public static readonly Guid RecordId = new("11111111-2222-3333-4444-555555555555");

    public const string BaseUrl = "https://example.api-us1.com/api/3/";
    public const string ApiKey = "ac-test-key-0123456789abcdef";

    public static IOptionsMonitor<ActiveCampaignOptions> Options(string? baseUrl = BaseUrl, string? apiKey = ApiKey) =>
        new StaticOptionsMonitor(new ActiveCampaignOptions { BaseUrl = baseUrl ?? "", ApiKey = apiKey ?? "" });

    /// <summary>A single-page form with one field per alias.</summary>
    public static Form Form(params string[] aliases)
    {
        var form = new Form { Id = Guid.NewGuid(), Name = "Newsletter sign-up" };
        var page = new Page { Caption = "Page 1" };
        var fieldset = new FieldSet { Id = Guid.NewGuid() };
        var container = new FieldsetContainer { Width = 12 };
        foreach (var alias in aliases)
            container.Fields.Add(new Field { Id = Guid.NewGuid(), Alias = alias, Caption = alias, Settings = new Dictionary<string, string>() });
        fieldset.Containers.Add(container);
        page.FieldSets.Add(fieldset);
        form.Pages.Add(page);
        return form;
    }

    /// <summary>A record for <paramref name="form"/>; object values are stored as-is, like Forms does at submit time.</summary>
    public static Record Record(Form form, IDictionary<string, object?> values)
    {
        var record = new Record { UniqueId = RecordId, Id = 42, Form = form.Id, Created = DateTime.UtcNow };
        foreach (var field in form.AllFields)
        {
            if (!values.TryGetValue(field.Alias, out var value)) continue;
            record.RecordFields[field.Id] = new RecordField(field)
            {
                Key = Guid.NewGuid(), Record = record.Id, Values = value is null ? [] : [value],
            };
        }
        return record;
    }

    /// <summary>
    /// Answers requests from a queue of canned responses (or throws a given exception) and records each request with
    /// its body, read eagerly because the workflow disposes the request once it has the response.
    /// </summary>
    internal sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<CapturedRequest> Requests { get; } = [];

        public ScriptedHandler Respond(HttpStatusCode status, string body)
        {
            _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body) });
            return this;
        }

        public ScriptedHandler Throw(Exception ex)
        {
            _responses.Enqueue(() => throw ex);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.TryGetValues("Api-Token", out var tokens) ? tokens.SingleOrDefault() : null,
                request.Content?.Headers.ContentType?.MediaType,
                body));

            if (_responses.Count == 0)
                throw new InvalidOperationException($"Unexpected request #{Requests.Count} to {request.RequestUri}");

            return _responses.Dequeue()();
        }
    }

    internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? ApiToken, string? ContentType, string? Body);

    /// <summary>Captures formatted log lines (and exception text) so tests can assert on what an operator would see.</summary>
    internal sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception is not null) message += Environment.NewLine + exception;
            Entries.Add((logLevel, message));
        }
    }

    private sealed class StaticOptionsMonitor(ActiveCampaignOptions value) : IOptionsMonitor<ActiveCampaignOptions>
    {
        public ActiveCampaignOptions CurrentValue => value;
        public ActiveCampaignOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ActiveCampaignOptions, string?> listener) => null;
    }
}
