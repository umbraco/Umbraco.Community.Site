using System.Net;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public int CallCount { get; private set; }

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public static StubHandler Json(string body) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    });

    public static StubHandler Throws() => new(_ => throw new HttpRequestException("simulated network error"));

    public static StubHandler Status(HttpStatusCode code) => new(_ => new HttpResponseMessage(code));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(_responder(request));
    }
}
