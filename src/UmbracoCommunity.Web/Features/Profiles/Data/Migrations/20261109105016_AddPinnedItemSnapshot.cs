using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedItemSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateUtc",
                table: "MemberPinnedItems",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MemberPinnedItems",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "MemberPinnedItems",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "MemberPinnedItems",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "MemberPinnedItems",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "MemberPinnedItems",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateUtc",
                table: "MemberPinnedItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MemberPinnedItems");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "MemberPinnedItems");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "MemberPinnedItems");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "MemberPinnedItems");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "MemberPinnedItems");
        }
    }
}
