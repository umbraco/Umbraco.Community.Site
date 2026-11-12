using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.SearchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddTermDayIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SearchTrackerTermDaily_Term_Day",
                table: "SearchTrackerTermDaily",
                columns: new[] { "Term", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SearchTrackerTermDaily_Term_Day",
                table: "SearchTrackerTermDaily");
        }
    }
}
