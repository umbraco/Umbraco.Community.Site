using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialPostJudgments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialPostJudgments",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PairKey = table.Column<string>(maxLength: 300, nullable: false),
                    Probability = table.Column<double>(nullable: false),
                    JudgedUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostJudgments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostJudgments_PairKey",
                table: "SocialPostJudgments",
                column: "PairKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialPostJudgments");
        }
    }
}
