namespace UmbracoCommunity.Web.Features.WebBotAuth;

public sealed class WebBotAuthOptions
{
    public const string SectionName = "WebBotAuth";

    /// <summary>
    /// Ed25519 private key (PKCS8 PEM) whose public half is published in the key directory.
    /// A secret: set it in appsettings.Local.json or the environment, never commit it.
    /// When empty the directory endpoint returns 404.
    /// </summary>
    public string? SigningPrivateKey { get; set; }

    /// <summary>How long a response signature stays valid, in seconds.</summary>
    public int SignatureLifetimeSeconds { get; set; } = 300;

    /// <summary>Cache-Control max-age for the directory response. Must stay below the signature lifetime.</summary>
    public int CacheMaxAgeSeconds { get; set; } = 60;
}
