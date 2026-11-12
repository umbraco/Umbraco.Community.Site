using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.SearchTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchTrackerTermDaily",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteKey = table.Column<Guid>(nullable: false),
                    Day = table.Column<DateOnly>(nullable: false),
                    Term = table.Column<string>(maxLength: 200, nullable: false),
                    SearchCount = table.Column<int>(nullable: false),
                    ZeroResultCount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchTrackerTermDaily", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchTrackerTermDaily_Day",
                table: "SearchTrackerTermDaily",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_SearchTrackerTermDaily_SiteKey_Day_Term",
                table: "SearchTrackerTermDaily",
                columns: new[] { "SiteKey", "Day", "Term" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchTrackerTermDaily");
        }
    }
}
