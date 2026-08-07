using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentBlockSpacingAfterRem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SpacingAfterRem",
                table: "ContentBlocks",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpacingAfterRem",
                table: "ContentBlocks");
        }
    }
}
