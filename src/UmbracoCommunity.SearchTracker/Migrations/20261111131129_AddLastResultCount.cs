using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.SearchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLastResultCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastResultCount",
                table: "SearchTrackerTermDaily",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastResultCount",
                table: "SearchTrackerTermDaily");
        }
    }
}
