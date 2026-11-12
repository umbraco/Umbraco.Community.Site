using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideActivityGraph",
                table: "MemberProfiles",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideTimelineCards",
                table: "MemberProfiles",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NoCardActivityIds",
                table: "MemberProfiles",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "ProfileTimelinePreviews",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageUrlHash = table.Column<string>(maxLength: 64, nullable: false),
                    PageUrl = table.Column<string>(maxLength: 2048, nullable: false),
                    ImageUrl = table.Column<string>(maxLength: 2048, nullable: false),
                    Description = table.Column<string>(maxLength: 300, nullable: true),
                    CheckedUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileTimelinePreviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTimelinePreviews_PageUrlHash",
                table: "ProfileTimelinePreviews",
                column: "PageUrlHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileTimelinePreviews");

            migrationBuilder.DropColumn(
                name: "HideActivityGraph",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "HideTimelineCards",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "NoCardActivityIds",
                table: "MemberProfiles");
        }
    }
}
