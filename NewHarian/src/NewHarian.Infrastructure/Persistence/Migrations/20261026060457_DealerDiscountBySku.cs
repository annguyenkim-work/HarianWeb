using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DealerDiscountBySku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean product-level discount config; Admin reconfigures by SKU after deploy.
            migrationBuilder.Sql("""
                DELETE FROM "DealerDiscountOverrides";
                DELETE FROM "DealerDiscountRates";
                DELETE FROM "DealerDiscountGroupProducts";
                DELETE FROM "DealerDiscountTiers";
                DELETE FROM "DealerDiscountGroups";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DealerDiscountOverrides_Products_ProductId",
                table: "DealerDiscountOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_DealerDiscountRates_Products_ProductId",
                table: "DealerDiscountRates");

            migrationBuilder.DropTable(
                name: "DealerDiscountGroupProducts");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "DealerDiscountRates",
                newName: "ProductVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_DealerDiscountRates_ProductId",
                table: "DealerDiscountRates",
                newName: "IX_DealerDiscountRates_ProductVariantId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "DealerDiscountOverrides",
                newName: "ProductVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_DealerDiscountOverrides_ProductId",
                table: "DealerDiscountOverrides",
                newName: "IX_DealerDiscountOverrides_ProductVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_DealerDiscountOverrides_DealerId_TierId_ProductId",
                table: "DealerDiscountOverrides",
                newName: "IX_DealerDiscountOverrides_DealerId_TierId_ProductVariantId");

            migrationBuilder.CreateTable(
                name: "DealerDiscountGroupVariants",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    ProductVariantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerDiscountGroupVariants", x => new { x.GroupId, x.ProductVariantId });
                    table.ForeignKey(
                        name: "FK_DealerDiscountGroupVariants_DealerDiscountGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DealerDiscountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealerDiscountGroupVariants_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountGroupVariants_ProductVariantId",
                table: "DealerDiscountGroupVariants",
                column: "ProductVariantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DealerDiscountOverrides_ProductVariants_ProductVariantId",
                table: "DealerDiscountOverrides",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DealerDiscountRates_ProductVariants_ProductVariantId",
                table: "DealerDiscountRates",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DealerDiscountOverrides_ProductVariants_ProductVariantId",
                table: "DealerDiscountOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_DealerDiscountRates_ProductVariants_ProductVariantId",
                table: "DealerDiscountRates");

            migrationBuilder.DropTable(
                name: "DealerDiscountGroupVariants");

            migrationBuilder.RenameColumn(
                name: "ProductVariantId",
                table: "DealerDiscountRates",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_DealerDiscountRates_ProductVariantId",
                table: "DealerDiscountRates",
                newName: "IX_DealerDiscountRates_ProductId");

            migrationBuilder.RenameColumn(
                name: "ProductVariantId",
                table: "DealerDiscountOverrides",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_DealerDiscountOverrides_ProductVariantId",
                table: "DealerDiscountOverrides",
                newName: "IX_DealerDiscountOverrides_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_DealerDiscountOverrides_DealerId_TierId_ProductVariantId",
                table: "DealerDiscountOverrides",
                newName: "IX_DealerDiscountOverrides_DealerId_TierId_ProductId");

            migrationBuilder.CreateTable(
                name: "DealerDiscountGroupProducts",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerDiscountGroupProducts", x => new { x.GroupId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_DealerDiscountGroupProducts_DealerDiscountGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DealerDiscountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealerDiscountGroupProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountGroupProducts_ProductId",
                table: "DealerDiscountGroupProducts",
                column: "ProductId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DealerDiscountOverrides_Products_ProductId",
                table: "DealerDiscountOverrides",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DealerDiscountRates_Products_ProductId",
                table: "DealerDiscountRates",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
