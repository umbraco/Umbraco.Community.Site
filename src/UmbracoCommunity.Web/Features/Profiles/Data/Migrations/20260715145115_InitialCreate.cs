using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitHubHandle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Bio = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AvatarMediaKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    OnboardingStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    OnboardingStartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OnboardingCompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SphereProfileId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberFeeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRemoved = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemovedReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RemovedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AddedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SphereSyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSphereSyncAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSphereSyncError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberFeeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberFeeds_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberFeeds_MemberProfileId",
                table: "MemberFeeds",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_GitHubHandle",
                table: "MemberProfiles",
                column: "GitHubHandle");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_MemberKey",
                table: "MemberProfiles",
                column: "MemberKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberFeeds");

            migrationBuilder.DropTable(
                name: "MemberProfiles");
        }
    }
}
