using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UmbracoCommunity.Web.Features.Members;

public class RegisterGitHubAuth : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        var clientId = builder.Config["GitHub:ClientId"] ?? string.Empty;
        var clientSecret = builder.Config["GitHub:ClientSecret"] ?? string.Empty;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return;

        builder.AddMemberExternalLogins(logins =>
        {
            logins.AddMemberLogin(
                auth => auth.AddOAuth<GitHubAuthenticationOptions, GitHubAuthenticationHandler>(
                    GitHubExternalLoginProviderOptions.SchemeName,
                    "GitHub",
                    options =>
                    {
                        options.ClientId = clientId;
                        options.ClientSecret = clientSecret;
                        options.Scope.Add("read:user");
                        options.Scope.Add("user:email");

                        options.Events.OnCreatingTicket = async context =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                            request.Headers.UserAgent.ParseAdd("UmbracoCommunity/1.0");
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                            var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                            if (!response.IsSuccessStatusCode) return;

                            var json = await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted);
                            var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(json,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            var primary = emails?.FirstOrDefault(e => e.Primary && e.Verified);
                            if (primary == null) return;

                            context.Identity?.TryRemoveClaim(context.Identity.FindFirst(ClaimTypes.Email));
                            context.Identity?.AddClaim(new Claim(ClaimTypes.Email, primary.Email));
                        };
                    }),
                GitHubExternalLoginProviderOptions.Configure);
        });
    }

    private sealed record GitHubEmail(string Email, bool Primary, bool Verified);
}
