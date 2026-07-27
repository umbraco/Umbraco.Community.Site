using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.BlogAnnouncements.Migrations
{
    /// <inheritdoc />
    public partial class RenamePlatformPostIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SphereId",
                table: "AnnouncedBlogPosts",
                newName: "PlatformPostId");

            migrationBuilder.RenameColumn(
                name: "SphereId",
                table: "AnnouncementAttempts",
                newName: "PlatformPostId");

            migrationBuilder.RenameIndex(
                name: "IX_AnnouncementAttempts_SphereId",
                table: "AnnouncementAttempts",
                newName: "IX_AnnouncementAttempts_PlatformPostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_AnnouncementAttempts_PlatformPostId",
                table: "AnnouncementAttempts",
                newName: "IX_AnnouncementAttempts_SphereId");

            migrationBuilder.RenameColumn(
                name: "PlatformPostId",
                table: "AnnouncementAttempts",
                newName: "SphereId");

            migrationBuilder.RenameColumn(
                name: "PlatformPostId",
                table: "AnnouncedBlogPosts",
                newName: "SphereId");
        }
    }
}
