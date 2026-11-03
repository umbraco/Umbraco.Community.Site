using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfileMatches",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(nullable: false),
                    SourceUrl = table.Column<string>(maxLength: 450, nullable: false),
                    State = table.Column<int>(nullable: false),
                    Confidence = table.Column<double>(nullable: false),
                    SignalsJson = table.Column<string>(maxLength: 4000, nullable: false),
                    DecidedBy = table.Column<int>(nullable: false),
                    DecidedByUserKey = table.Column<Guid>(nullable: true),
                    DecidedByUserName = table.Column<string>(maxLength: 200, nullable: true),
                    DecidedUtc = table.Column<DateTime>(nullable: false),
                    CreatedUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileMatches_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileMatches_MemberProfileId_SourceUrl",
                table: "ProfileMatches",
                columns: new[] { "MemberProfileId", "SourceUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileMatches_State",
                table: "ProfileMatches",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileMatches");
        }
    }
}
