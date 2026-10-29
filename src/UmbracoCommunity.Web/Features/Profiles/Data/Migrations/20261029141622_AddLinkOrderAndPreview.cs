using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkOrderAndPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaviconUrl",
                table: "MemberFeeds",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviewCheckedUtc",
                table: "MemberFeeds",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviewImageUrl",
                table: "MemberFeeds",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviewStatus",
                table: "MemberFeeds",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PreviewTitle",
                table: "MemberFeeds",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "MemberFeeds",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaviconUrl",
                table: "MemberFeeds");

            migrationBuilder.DropColumn(
                name: "PreviewCheckedUtc",
                table: "MemberFeeds");

            migrationBuilder.DropColumn(
                name: "PreviewImageUrl",
                table: "MemberFeeds");

            migrationBuilder.DropColumn(
                name: "PreviewStatus",
                table: "MemberFeeds");

            migrationBuilder.DropColumn(
                name: "PreviewTitle",
                table: "MemberFeeds");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "MemberFeeds");
        }
    }
}
