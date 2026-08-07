using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HeaderMainMenuItemKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_MenuId",
                table: "MenuItems");

            migrationBuilder.AddColumn<string>(
                name: "ItemKey",
                table: "MenuItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuId_ItemKey",
                table: "MenuItems",
                columns: new[] { "MenuId", "ItemKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_MenuId_ItemKey",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ItemKey",
                table: "MenuItems");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuId",
                table: "MenuItems",
                column: "MenuId");
        }
    }
}
