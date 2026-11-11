using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberActivityEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberActivityEntries",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(nullable: false),
                    Kind = table.Column<string>(maxLength: 50, nullable: false),
                    Title = table.Column<string>(maxLength: 200, nullable: false),
                    Url = table.Column<string>(maxLength: 2048, nullable: false),
                    DateUtc = table.Column<DateTime>(nullable: false),
                    Description = table.Column<string>(maxLength: 500, nullable: true),
                    State = table.Column<int>(nullable: false),
                    RejectionReason = table.Column<string>(maxLength: 500, nullable: true),
                    DecidedByUserKey = table.Column<Guid>(nullable: true),
                    DecidedByUserName = table.Column<string>(maxLength: 200, nullable: true),
                    DecidedUtc = table.Column<DateTime>(nullable: true),
                    CreatedUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberActivityEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberActivityEntries_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivityEntries_MemberProfileId",
                table: "MemberActivityEntries",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivityEntries_State",
                table: "MemberActivityEntries",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberActivityEntries");
        }
    }
}
