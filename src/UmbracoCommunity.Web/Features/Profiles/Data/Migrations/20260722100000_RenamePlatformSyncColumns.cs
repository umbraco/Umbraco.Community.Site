using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePlatformSyncColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SphereProfileId",
                table: "MemberProfiles",
                newName: "PlatformProfileId");

            migrationBuilder.RenameColumn(
                name: "SphereSyncStatus",
                table: "MemberFeeds",
                newName: "PlatformSyncStatus");

            migrationBuilder.RenameColumn(
                name: "LastSphereSyncError",
                table: "MemberFeeds",
                newName: "LastPlatformSyncError");

            migrationBuilder.RenameColumn(
                name: "LastSphereSyncAttemptUtc",
                table: "MemberFeeds",
                newName: "LastPlatformSyncAttemptUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlatformProfileId",
                table: "MemberProfiles",
                newName: "SphereProfileId");

            migrationBuilder.RenameColumn(
                name: "PlatformSyncStatus",
                table: "MemberFeeds",
                newName: "SphereSyncStatus");

            migrationBuilder.RenameColumn(
                name: "LastPlatformSyncError",
                table: "MemberFeeds",
                newName: "LastSphereSyncError");

            migrationBuilder.RenameColumn(
                name: "LastPlatformSyncAttemptUtc",
                table: "MemberFeeds",
                newName: "LastSphereSyncAttemptUtc");
        }
    }
}
