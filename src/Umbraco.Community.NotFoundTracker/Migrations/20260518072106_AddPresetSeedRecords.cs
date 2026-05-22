using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.NotFoundTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPresetSeedRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotFoundPresetSeedRecords",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hostname = table.Column<string>(maxLength: 255, nullable: true),
                    MatchType = table.Column<byte>(nullable: false),
                    Path = table.Column<string>(maxLength: 2048, nullable: false),
                    SeededUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundPresetSeedRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundPresetSeedRecords_Hostname",
                table: "NotFoundPresetSeedRecords",
                column: "Hostname");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotFoundPresetSeedRecords");
        }
    }
}
