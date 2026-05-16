using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.NotFoundTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotFoundHits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    HitCount = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundHits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotFoundIgnoreRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MatchType = table.Column<byte>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Source = table.Column<byte>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundIgnoreRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotFoundHitQueryStrings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HitId = table.Column<int>(type: "INTEGER", nullable: false),
                    QueryString = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    HitCount = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundHitQueryStrings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotFoundHitQueryStrings_NotFoundHits_HitId",
                        column: x => x.HitId,
                        principalTable: "NotFoundHits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHitQueryStrings_HitId_QueryString",
                table: "NotFoundHitQueryStrings",
                columns: new[] { "HitId", "QueryString" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHitQueryStrings_LastSeenUtc",
                table: "NotFoundHitQueryStrings",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHits_HitCount",
                table: "NotFoundHits",
                column: "HitCount");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHits_Hostname_Path",
                table: "NotFoundHits",
                columns: new[] { "Hostname", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHits_LastSeenUtc",
                table: "NotFoundHits",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundIgnoreRules_Hostname_MatchType_Path",
                table: "NotFoundIgnoreRules",
                columns: new[] { "Hostname", "MatchType", "Path" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotFoundHitQueryStrings");

            migrationBuilder.DropTable(
                name: "NotFoundIgnoreRules");

            migrationBuilder.DropTable(
                name: "NotFoundHits");
        }
    }
}
