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
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hostname = table.Column<string>(maxLength: 255, nullable: false),
                    Path = table.Column<string>(maxLength: 2048, nullable: false),
                    HitCount = table.Column<long>(nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(nullable: false),
                    LastSeenUtc = table.Column<DateTime>(nullable: false),
                    LastUserAgent = table.Column<string>(maxLength: 512, nullable: true),
                    Status = table.Column<byte>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundHits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotFoundIgnoreRules",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hostname = table.Column<string>(maxLength: 255, nullable: true),
                    MatchType = table.Column<byte>(nullable: false),
                    Path = table.Column<string>(maxLength: 2048, nullable: false),
                    Source = table.Column<byte>(nullable: false),
                    Note = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotFoundIgnoreRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotFoundHitQueryStrings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HitId = table.Column<int>(nullable: false),
                    QueryString = table.Column<string>(maxLength: 2048, nullable: false),
                    HitCount = table.Column<long>(nullable: false),
                    LastSeenUtc = table.Column<DateTime>(nullable: false)
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
                name: "IX_NotFoundHitQueryStrings_HitId",
                table: "NotFoundHitQueryStrings",
                column: "HitId");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHitQueryStrings_LastSeenUtc",
                table: "NotFoundHitQueryStrings",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHits_HitCount",
                table: "NotFoundHits",
                column: "HitCount");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHits_Hostname",
                table: "NotFoundHits",
                column: "Hostname");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundHits_LastSeenUtc",
                table: "NotFoundHits",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotFoundIgnoreRules_Hostname",
                table: "NotFoundIgnoreRules",
                column: "Hostname");
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
