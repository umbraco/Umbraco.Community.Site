using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>Builds the <see cref="ProfileEditSubjectResolver"/> the member profile API controllers take.</summary>
internal static class TestProfileEditSubjects
{
    public static ProfileEditSubjectResolver Resolver(IMemberManager memberManager, ProfileAccessService? access = null, MemberProfileStore? store = null) =>
        new(
            memberManager,
            access ?? new ProfileAccessService(memberManager, Mock.Of<IProfileDataProvider>(), NullLogger<ProfileAccessService>.Instance),
            store ?? new MemberProfileStore(null!),
            NullLogger<ProfileEditSubjectResolver>.Instance);
}
