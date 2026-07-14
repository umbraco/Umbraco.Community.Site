using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.BlogAnnouncements.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnouncedBlogPosts",
                columns: table => new
                {
                    SphereId = table.Column<Guid>(nullable: false),
                    Url = table.Column<string>(maxLength: 2048, nullable: false),
                    Title = table.Column<string>(maxLength: 1024, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(nullable: false),
                    Fingerprint = table.Column<string>(maxLength: 450, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(nullable: false),
                    AnnouncedUtc = table.Column<DateTime>(nullable: true),
                    Status = table.Column<byte>(nullable: false),
                    AuthorName = table.Column<string>(maxLength: 512, nullable: true),
                    AuthorAvatarUrl = table.Column<string>(maxLength: 2048, nullable: true),
                    AuthorProfileUrl = table.Column<string>(maxLength: 2048, nullable: true),
                    Excerpt = table.Column<string>(nullable: true),
                    CoverImageUrl = table.Column<string>(maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncedBlogPosts", x => x.SphereId);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementRuns",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunUtc = table.Column<DateTime>(nullable: false),
                    Fetched = table.Column<int>(nullable: false),
                    New = table.Column<int>(nullable: false),
                    Announced = table.Column<int>(nullable: false),
                    Skipped = table.Column<int>(nullable: false),
                    Failed = table.Column<int>(nullable: false),
                    DryRun = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SphereId = table.Column<Guid>(nullable: false),
                    AttemptedUtc = table.Column<DateTime>(nullable: false),
                    Outcome = table.Column<string>(maxLength: 64, nullable: false),
                    HttpStatus = table.Column<int>(nullable: true),
                    Trigger = table.Column<byte>(nullable: false),
                    Destination = table.Column<string>(maxLength: 64, nullable: false, defaultValue: "Discord")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAttempts_AnnouncedBlogPosts_SphereId",
                        column: x => x.SphereId,
                        principalTable: "AnnouncedBlogPosts",
                        principalColumn: "SphereId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncedBlogPosts_Fingerprint",
                table: "AnnouncedBlogPosts",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncedBlogPosts_PublishedAtUtc",
                table: "AnnouncedBlogPosts",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncedBlogPosts_Status",
                table: "AnnouncedBlogPosts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAttempts_AttemptedUtc",
                table: "AnnouncementAttempts",
                column: "AttemptedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAttempts_SphereId",
                table: "AnnouncementAttempts",
                column: "SphereId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementRuns_RunUtc",
                table: "AnnouncementRuns",
                column: "RunUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementAttempts");

            migrationBuilder.DropTable(
                name: "AnnouncementRuns");

            migrationBuilder.DropTable(
                name: "AnnouncedBlogPosts");
        }
    }
}
