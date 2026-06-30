using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.Security;

namespace UmbracoCommunity.Web.Features.Members;

public class RegisterGitHubAuth : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddMemberExternalLogins(logins =>
        {
            logins.AddMemberExternalLogin(login =>
            {
                login.AuthenticationScheme = GitHubExternalLoginProviderOptions.SchemeName;
            });
        });

        builder.Services.ConfigureOptions<GitHubExternalLoginProviderOptions>();

        builder.Services
            .AddAuthentication()
            .AddGitHub(GitHubExternalLoginProviderOptions.SchemeName, options =>
            {
                var config = builder.Services
                    .BuildServiceProvider()
                    .GetRequiredService<IConfiguration>();

                options.ClientId = config["GitHub:ClientId"] ?? string.Empty;
                options.ClientSecret = config["GitHub:ClientSecret"] ?? string.Empty;
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

                    context.Identity?.RemoveClaim(context.Identity.FindFirst(ClaimTypes.Email));
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Email, primary.Email));
                };
            });
    }

    private sealed record GitHubEmail(string Email, bool Primary, bool Verified);
}
