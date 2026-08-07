using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductHidePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HidePrice",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HidePrice",
                table: "Products");
        }
    }
}
