using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialGitHubDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitHubDiscussions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RepositoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubDiscussions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubHqMembers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Login = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubHqMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RepositoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubPullRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RepositoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubPullRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubIssueReleases",
                columns: table => new
                {
                    IssueId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReleaseLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubIssueReleases", x => new { x.IssueId, x.ReleaseLabel });
                    table.ForeignKey(
                        name: "FK_GitHubIssueReleases_GitHubIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "GitHubIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GitHubPullRequestReleases",
                columns: table => new
                {
                    PullRequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReleaseLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubPullRequestReleases", x => new { x.PullRequestId, x.ReleaseLabel });
                    table.ForeignKey(
                        name: "FK_GitHubPullRequestReleases_GitHubPullRequests_PullRequestId",
                        column: x => x.PullRequestId,
                        principalTable: "GitHubPullRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubDiscussions_CreatedAt",
                table: "GitHubDiscussions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubDiscussions_RepositoryName_Number",
                table: "GitHubDiscussions",
                columns: new[] { "RepositoryName", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubHqMembers_Login",
                table: "GitHubHqMembers",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubIssueReleases_ReleaseLabel",
                table: "GitHubIssueReleases",
                column: "ReleaseLabel");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubIssues_CreatedAt",
                table: "GitHubIssues",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubIssues_RepositoryName_Number",
                table: "GitHubIssues",
                columns: new[] { "RepositoryName", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequestReleases_ReleaseLabel",
                table: "GitHubPullRequestReleases",
                column: "ReleaseLabel");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_CreatedAt",
                table: "GitHubPullRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubPullRequests_RepositoryName_Number",
                table: "GitHubPullRequests",
                columns: new[] { "RepositoryName", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubDiscussions");

            migrationBuilder.DropTable(
                name: "GitHubHqMembers");

            migrationBuilder.DropTable(
                name: "GitHubIssueReleases");

            migrationBuilder.DropTable(
                name: "GitHubPullRequestReleases");

            migrationBuilder.DropTable(
                name: "GitHubIssues");

            migrationBuilder.DropTable(
                name: "GitHubPullRequests");
        }
    }
}
