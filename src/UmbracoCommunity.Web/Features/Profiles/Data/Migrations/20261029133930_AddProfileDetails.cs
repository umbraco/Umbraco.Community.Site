using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayNameOverride",
                table: "MemberProfiles",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubBio",
                table: "MemberProfiles",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubCompany",
                table: "MemberProfiles",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GitHubFetchedUtc",
                table: "MemberProfiles",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                table: "MemberProfiles",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayNameOverride",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "GitHubBio",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "GitHubCompany",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "GitHubFetchedUtc",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "MemberProfiles");
        }
    }
}
