using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoCommunity.BlockRestrictions.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockRestrictionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentTypeKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllowedBlockAliasesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockRestrictionRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockRestrictionRules_DocumentTypeKey",
                table: "BlockRestrictionRules",
                column: "DocumentTypeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockRestrictionRules");
        }
    }
}
