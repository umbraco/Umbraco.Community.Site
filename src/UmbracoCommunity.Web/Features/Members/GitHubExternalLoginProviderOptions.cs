using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;

namespace UmbracoCommunity.Web.Features.Members;

internal sealed class GitHubExternalLoginProviderOptions : IConfigureNamedOptions<MemberExternalLoginProviderOptions>
{
    public const string SchemeName = "GitHub";
    private const string MemberTypeAlias = "communityMember";
    private const string DefaultGroup = "Community Members";

    public void Configure(string? name, MemberExternalLoginProviderOptions options)
    {
        if (name != SchemeName) return;

        options.AutoLinkOptions = new MemberExternalSignInAutoLinkOptions(
            autoLinkExternalAccount: true,
            defaultUserGroups: [DefaultGroup],
            defaultMemberTypeAlias: MemberTypeAlias)
        {
            OnAutoLinking = (autoLinkUser, loginInfo) =>
            {
                var email = loginInfo.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var handle = loginInfo.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var displayName = loginInfo.Principal.FindFirst("urn:github:name")?.Value ?? handle;
                var avatarUrl = loginInfo.Principal.FindFirst("urn:github:avatar_url")?.Value
                              ?? (handle != null ? $"https://github.com/{handle}.png" : null);

                autoLinkUser.Name = displayName;
                autoLinkUser.UserName = handle;
                autoLinkUser.Email = email;
                autoLinkUser.IsApproved = true;

                if (avatarUrl != null)
                    autoLinkUser.AdditionalData["avatarUrl"] = avatarUrl;
            },

            OnExternalLogin = (user, loginInfo) =>
            {
                var email = loginInfo.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email)) return false;

                var handle = loginInfo.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var displayName = loginInfo.Principal.FindFirst("urn:github:name")?.Value ?? handle;
                var avatarUrl = loginInfo.Principal.FindFirst("urn:github:avatar_url")?.Value
                              ?? (handle != null ? $"https://github.com/{handle}.png" : null);

                user.Name = displayName;
                user.UserName = handle;
                user.Email = email;

                if (avatarUrl != null)
                    user.AdditionalData["avatarUrl"] = avatarUrl;

                return true;
            }
        };
    }

    public void Configure(MemberExternalLoginProviderOptions options) => throw new NotImplementedException();
}
