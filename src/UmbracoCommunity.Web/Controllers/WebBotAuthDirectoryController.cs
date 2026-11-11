using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Features.WebBotAuth;

namespace UmbracoCommunity.Web.Controllers;

/// <summary>
/// Serves the signed Web Bot Auth key directory at
/// /.well-known/http-message-signatures-directory (routed in UseApplicationEndpoints).
/// </summary>
public class WebBotAuthDirectoryController : Controller
{
    private const string ContentType = "application/http-message-signatures-directory+json";

    private readonly KeyDirectoryService _directory;

    public WebBotAuthDirectoryController(KeyDirectoryService directory) => _directory = directory;

    public IActionResult Index()
    {
        if (!_directory.IsConfigured)
        {
            return NotFound();
        }

        var signed = _directory.Sign(Request.Host.Value ?? string.Empty);

        Response.Headers["Content-Digest"] = signed.ContentDigest;
        Response.Headers["Signature-Input"] = signed.SignatureInput;
        Response.Headers["Signature"] = signed.Signature;
        Response.Headers.CacheControl = $"public, max-age={_directory.CacheMaxAgeSeconds}";

        return Content(signed.Body, ContentType);
    }
}
