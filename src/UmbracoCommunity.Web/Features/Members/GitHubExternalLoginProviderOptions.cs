using System.Security.Claims;
using Umbraco.Cms.Web.Common.Security;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Features.Members;

public sealed class GitHubExternalLoginProviderOptions
{
    public const string SchemeName = "UmbracoMembers.GitHub";
    public const string DefaultGroup = "Community Members";

    internal static void Configure(MemberExternalLoginProviderOptions options)
    {
        // New members are created as lightweight external members (umbracoExternalMember table,
        // no content node), because GitHub is the source of truth for their identity and all
        // profile data lives in MemberProfileStore. The member type alias is still required by
        // the constructor but is not used for external-only members.
        //
        // Existing content-based members are unaffected: an existing GitHub login link still
        // resolves to the content member, and ExternalOnly only applies when a new member is
        // auto-created. See ExternalMemberMigrationService to convert existing members.
        options.AutoLinkOptions = new MemberExternalSignInAutoLinkOptions(
            true, false, CommunityMember.ModelTypeAlias, null, [DefaultGroup])
        {
            ExternalOnly = true,

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

            // For external-only members Umbraco syncs Email, UserName (set to the email!) and
            // Name (set to the GitHub login) from the claims just before this callback runs.
            // The whole site keys profiles and URLs off UserName being the GitHub handle, so
            // this must keep overwriting UserName and Name; the sign-in then persists them.
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
