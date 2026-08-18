using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Community.FormsSpamGuard.Configuration;
using Umbraco.Community.FormsSpamGuard.Signals;
using Umbraco.Community.FormsSpamGuard.Tokens;

namespace Umbraco.Community.FormsSpamGuard.Tests;

/// <summary>
/// Shared fixtures. Deliberately hand-rolled rather than pulling in a mocking or time-testing package for what
/// amounts to a few lines each.
/// </summary>
internal static class TestHelpers
{
    public static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    public static SpamGuardTokenService CreateTokenService(DateTimeOffset? now = null) =>
        new(
            new EphemeralDataProtectionProvider(),
            Options.Create(new FormsSpamGuardOptions()),
            new FixedTimeProvider(now ?? Now));

    public static SpamGuardToken CreateToken(
        DateTimeOffset? renderedUtc = null,
        string decoyFieldName = "emailConfirm_abc123",
        string nonce = "0123456789abcdef") =>
        new(renderedUtc ?? Now, decoyFieldName, nonce);

    public static SpamSignalContext CreateContext(
        SpamGuardToken? token = null,
        SpamGuardFieldSettings? settings = null,
        DateTimeOffset? now = null,
        string fieldKey = "11111111-1111-1111-1111-111111111111",
        Dictionary<string, string>? postedValues = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(
            (postedValues ?? []).ToDictionary(x => x.Key, x => new Microsoft.Extensions.Primitives.StringValues(x.Value)));

        return new SpamSignalContext(
            token ?? CreateToken(),
            settings ?? new SpamGuardFieldSettings(),
            httpContext,
            fieldKey,
            now ?? Now);
    }

    /// <summary>A <see cref="TimeProvider"/> pinned to a fixed instant.</summary>
    public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
