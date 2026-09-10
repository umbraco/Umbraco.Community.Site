using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.MeetBooking.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetProvisioningStates",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(nullable: false),
                    StateJson = table.Column<string>(nullable: false),
                    LastError = table.Column<string>(nullable: true),
                    UpdatedUtc = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetProvisioningStates", x => x.RecordId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetProvisioningStates_UpdatedUtc",
                table: "MeetProvisioningStates",
                column: "UpdatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetProvisioningStates");
        }
    }
}
