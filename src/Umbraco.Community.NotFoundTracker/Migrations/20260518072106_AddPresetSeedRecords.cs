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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MatchType = table.Column<byte>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SeededUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundPresetSeedRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundPresetSeedRecords_Hostname_MatchType_Path",
                table: "NotFoundPresetSeedRecords",
                columns: new[] { "Hostname", "MatchType", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotFoundPresetSeedRecords");
        }
    }
}
