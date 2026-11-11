using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.Web.Features.Profiles.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileDisplayChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HiddenActivityIds",
                table: "MemberProfiles",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "HiddenSections",
                table: "MemberProfiles",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "MemberPinnedItems",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(nullable: false),
                    ActivityId = table.Column<string>(maxLength: 100, nullable: false),
                    Reason = table.Column<string>(maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPinnedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberPinnedItems_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPinnedItems_MemberProfileId",
                table: "MemberPinnedItems",
                column: "MemberProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberPinnedItems");

            migrationBuilder.DropColumn(
                name: "HiddenActivityIds",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "HiddenSections",
                table: "MemberProfiles");
        }
    }
}
