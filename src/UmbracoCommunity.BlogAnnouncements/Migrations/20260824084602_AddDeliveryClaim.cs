using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.BlogAnnouncements.Migrations
{
    /// <summary>
    /// Adds the delivery-claim timestamp behind <c>AnnouncementStatus.Claimed</c> — see
    /// <c>Infrastructure/AnnouncementClaims.cs</c> for what it guards against. The Status column
    /// itself needs no change; Claimed is just another value of the existing byte enum.
    /// </summary>
    public partial class AddDeliveryClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedUtc",
                table: "AnnouncedBlogPosts",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedUtc",
                table: "AnnouncedBlogPosts");
        }
    }
}
