using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The account page as a profile admin sees it while editing another member's profile (see
/// <see cref="ProfileEditSubjectResolver"/>): the member's own data and editors, a banner saying whose profile it is,
/// and nothing that is the member's alone to see or do.
/// </summary>
public static class AccountPageEditAs
{
    /// <summary>Marks <paramref name="model"/> as an edit-as page for <paramref name="subject"/> and takes out what the admin may not see.</summary>
    public static void Apply(AccountPageViewModel model, ProfileEditSubject subject)
    {
        model.EditAsHandle = subject.Handle;

        // Never shown to anyone but the member.
        model.Email = null;
        model.LastSyncedAt = null;

        // Self-only sections.
        model.MvpBadgeYear = null;
        model.MvpBadgeDownloadUrl = null;
        model.MvpBadgeImageUrl = null;
    }
}
