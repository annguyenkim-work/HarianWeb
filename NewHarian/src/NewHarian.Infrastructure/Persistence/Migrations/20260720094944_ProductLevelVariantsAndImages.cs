using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductLevelVariantsAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImageMediaFileId",
                table: "ProductVariants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasVariantColor",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasVariantSize",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MainImageMediaFileId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ImageMediaFileId",
                table: "ProductVariants",
                column: "ImageMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_MainImageMediaFileId",
                table: "Products",
                column: "MainImageMediaFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_MediaFiles_MainImageMediaFileId",
                table: "Products",
                column: "MainImageMediaFileId",
                principalTable: "MediaFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_MediaFiles_ImageMediaFileId",
                table: "ProductVariants",
                column: "ImageMediaFileId",
                principalTable: "MediaFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE "Products" p
                SET "HasVariantSize" = c."HasVariantSize",
                    "HasVariantColor" = c."HasVariantColor"
                FROM "Categories" c
                WHERE p."CategoryId" = c."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "Products" p
                SET "MainImageMediaFileId" = sub."MediaFileId"
                FROM (
                    SELECT DISTINCT ON ("ProductId") "ProductId", "MediaFileId"
                    FROM "ProductImages"
                    ORDER BY "ProductId", "IsPrimary" DESC, "SortOrder", "Id"
                ) sub
                WHERE p."Id" = sub."ProductId";
                """);

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropColumn(
                name: "HasVariantColor",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "HasVariantSize",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_MediaFiles_MainImageMediaFileId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_MediaFiles_ImageMediaFileId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ImageMediaFileId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_MainImageMediaFileId",
                table: "Products");

            migrationBuilder.AddColumn<bool>(
                name: "HasVariantColor",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasVariantSize",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaFileId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_MediaFileId",
                table: "ProductImages",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.Sql("""
                UPDATE "Categories" c
                SET "HasVariantSize" = EXISTS (
                        SELECT 1 FROM "Products" p
                        WHERE p."CategoryId" = c."Id" AND p."HasVariantSize"),
                    "HasVariantColor" = EXISTS (
                        SELECT 1 FROM "Products" p
                        WHERE p."CategoryId" = c."Id" AND p."HasVariantColor");
                """);

            migrationBuilder.Sql("""
                INSERT INTO "ProductImages" ("ProductId", "MediaFileId", "IsPrimary", "SortOrder")
                SELECT p."Id", p."MainImageMediaFileId", TRUE, 1
                FROM "Products" p
                WHERE p."MainImageMediaFileId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "ImageMediaFileId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "HasVariantColor",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HasVariantSize",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MainImageMediaFileId",
                table: "Products");
        }
    }
}
