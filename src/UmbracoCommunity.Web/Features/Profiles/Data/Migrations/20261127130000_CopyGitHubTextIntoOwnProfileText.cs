using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <summary>
    /// Data only: profiles used to fall back to the cached GitHub company and bio when the member's own headline or bio
    /// was null, and to the GitHub name when they had not chosen their own. That fallback is gone (the member's own text
    /// is the only thing shown), so this copies the cached GitHub values into the member's own fields where those are
    /// still null, keeping exactly what each profile showed before. Afterwards an empty headline or bio simply isn't
    /// shown. It also turns the short-lived "show no headline" marker (an empty string) into null, which now means the same.
    /// </summary>
    /// <remarks>
    /// Plain ANSI SQL so it runs unchanged on SQLite and SQL Server. The column sizes match (Headline and GitHubCompany
    /// are both 200, Bio and GitHubBio both 1000, DisplayNameOverride and DisplayName both 200), so nothing needs cutting.
    /// The cached GitHub columns are kept. Down does nothing: once copied, the text is the member's own.
    /// </remarks>
    public partial class CopyGitHubTextIntoOwnProfileText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE MemberProfiles SET Headline = GitHubCompany " +
                "WHERE Headline IS NULL AND GitHubCompany IS NOT NULL AND GitHubCompany <> ''");

            migrationBuilder.Sql(
                "UPDATE MemberProfiles SET Bio = GitHubBio " +
                "WHERE Bio IS NULL AND GitHubBio IS NOT NULL AND GitHubBio <> ''");

            migrationBuilder.Sql(
                "UPDATE MemberProfiles SET DisplayNameOverride = DisplayName " +
                "WHERE DisplayNameOverride IS NULL AND DisplayName <> ''");

            // After the copy above, so a headline the member chose to hide stays hidden rather than being refilled.
            migrationBuilder.Sql("UPDATE MemberProfiles SET Headline = NULL WHERE Headline = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
