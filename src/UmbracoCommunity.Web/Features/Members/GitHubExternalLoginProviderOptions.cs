using System.Security.Claims;
using Umbraco.Cms.Web.Common.Security;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Features.Members;

public sealed class GitHubExternalLoginProviderOptions
{
    public const string SchemeName = "UmbracoMembers.GitHub";
    private const string DefaultGroup = "Community Members";

    internal static void Configure(MemberExternalLoginProviderOptions options)
    {
        options.AutoLinkOptions = new MemberExternalSignInAutoLinkOptions(
            true, false, CommunityMember.ModelTypeAlias, null, [DefaultGroup])
        {
            OnAutoLinking = (autoLinkUser, loginInfo) =>
            {
                var email = loginInfo.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var handle = loginInfo.Principal.FindFirst(ClaimTypes.Name)?.Value;
                var displayName = loginInfo.Principal.FindFirst("urn:github:name")?.Value ?? handle;

                autoLinkUser.Name = displayName;
                autoLinkUser.UserName = handle;
                autoLinkUser.Email = email;
                autoLinkUser.IsApproved = true;
            },

            OnExternalLogin = (user, loginInfo) =>
            {
                var email = loginInfo.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var handle = loginInfo.Principal.FindFirst(ClaimTypes.Name)?.Value;
                var displayName = loginInfo.Principal.FindFirst("urn:github:name")?.Value ?? handle;

                user.Name = displayName;
                user.UserName = handle;
                if (!string.IsNullOrEmpty(email))
                {
                    user.Email = email;
                }

                return true;
            }
        };
    }
}
