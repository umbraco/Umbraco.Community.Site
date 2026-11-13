using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

namespace UmbracoCommunity.Web.Tests.Features.OpenBadges;

internal static class OpenBadgesTestData
{
    public const string SigningKey = "test-signing-key-that-is-long-enough-0123456789";

    public static readonly Uri BaseUri = new("https://community.example/");

    public static OpenBadgesOptions Options(string? signingKey = SigningKey) => new() { SigningKey = signingKey };

    public static MvpBadgeTokenService Tokens(string? signingKey = SigningKey) =>
        new(Microsoft.Extensions.Options.Options.Create(Options(signingKey)), NullLogger<MvpBadgeTokenService>.Instance);
}
