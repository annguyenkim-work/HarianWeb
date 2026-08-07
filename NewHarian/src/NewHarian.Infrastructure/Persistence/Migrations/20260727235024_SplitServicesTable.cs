using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitServicesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Detach bookings from Products / ProductVariants
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceBookings_ProductVariants_ProductVariantId",
                table: "ServiceBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceBookings_Products_ProductId",
                table: "ServiceBookings");

            // 2) Create Services tables (before dropping ProductType — need it for data copy)
            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    HasVariantSize = table.Column<bool>(type: "boolean", nullable: false),
                    HasVariantColor = table.Column<bool>(type: "boolean", nullable: false),
                    HidePrice = table.Column<bool>(type: "boolean", nullable: false),
                    MainImageMediaFileId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Services_MediaFiles_MainImageMediaFileId",
                        column: x => x.MainImageMediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ShortDescription = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MetaTitle = table.Column<string>(type: "text", nullable: true),
                    MetaDescription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTranslations_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VariantLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorDefinitionId = table.Column<int>(type: "integer", nullable: true),
                    ImageMediaFileId = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompareAtPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceVariants_ColorDefinitions_ColorDefinitionId",
                        column: x => x.ColorDefinitionId,
                        principalTable: "ColorDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceVariants_MediaFiles_ImageMediaFileId",
                        column: x => x.ImageMediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceVariants_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_CategoryId_Slug",
                table: "Services",
                columns: new[] { "CategoryId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_MainImageMediaFileId",
                table: "Services",
                column: "MainImageMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTranslations_ServiceId_LanguageCode",
                table: "ServiceTranslations",
                columns: new[] { "ServiceId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVariants_ColorDefinitionId",
                table: "ServiceVariants",
                column: "ColorDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVariants_ImageMediaFileId",
                table: "ServiceVariants",
                column: "ImageMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVariants_ServiceId",
                table: "ServiceVariants",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVariants_Sku",
                table: "ServiceVariants",
                column: "Sku",
                unique: true);

            // 3) Copy ProductType=1 rows into Services (preserve Ids so bookings stay valid)
            migrationBuilder.Sql("""
                INSERT INTO "Services" (
                    "Id", "CategoryId", "Slug", "Status", "IsFeatured", "SortOrder",
                    "HasVariantSize", "HasVariantColor", "HidePrice", "MainImageMediaFileId",
                    "CreatedAt", "UpdatedAt")
                SELECT
                    p."Id", p."CategoryId", p."Slug", p."Status", p."IsFeatured", p."SortOrder",
                    p."HasVariantSize", p."HasVariantColor", p."HidePrice", p."MainImageMediaFileId",
                    p."CreatedAt", p."UpdatedAt"
                FROM "Products" p
                WHERE p."ProductType" = 1;

                INSERT INTO "ServiceTranslations" (
                    "Id", "ServiceId", "LanguageCode", "Name", "ShortDescription",
                    "Description", "MetaTitle", "MetaDescription")
                SELECT
                    t."Id", t."ProductId", t."LanguageCode", t."Name", t."ShortDescription",
                    t."Description", t."MetaTitle", t."MetaDescription"
                FROM "ProductTranslations" t
                INNER JOIN "Products" p ON p."Id" = t."ProductId"
                WHERE p."ProductType" = 1;

                INSERT INTO "ServiceVariants" (
                    "Id", "ServiceId", "Sku", "VariantLabel", "ColorDefinitionId", "ImageMediaFileId",
                    "Price", "CompareAtPrice", "IsDefault", "SortOrder", "IsActive")
                SELECT
                    v."Id", v."ProductId", v."Sku", v."VariantLabel", v."ColorDefinitionId", v."ImageMediaFileId",
                    v."Price", v."CompareAtPrice", v."IsDefault", v."SortOrder", v."IsActive"
                FROM "ProductVariants" v
                INNER JOIN "Products" p ON p."Id" = v."ProductId"
                WHERE p."ProductType" = 1;

                SELECT setval(pg_get_serial_sequence('"Services"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Services"), 1));
                SELECT setval(pg_get_serial_sequence('"ServiceTranslations"', 'Id'), COALESCE((SELECT MAX("Id") FROM "ServiceTranslations"), 1));
                SELECT setval(pg_get_serial_sequence('"ServiceVariants"', 'Id'), COALESCE((SELECT MAX("Id") FROM "ServiceVariants"), 1));
                """);

            // 4) Rename booking FKs to Services
            migrationBuilder.RenameColumn(
                name: "ProductVariantId",
                table: "ServiceBookings",
                newName: "ServiceVariantId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "ServiceBookings",
                newName: "ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceBookings_ProductVariantId",
                table: "ServiceBookings",
                newName: "IX_ServiceBookings_ServiceVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceBookings_ProductId",
                table: "ServiceBookings",
                newName: "IX_ServiceBookings_ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBookings_ServiceVariants_ServiceVariantId",
                table: "ServiceBookings",
                column: "ServiceVariantId",
                principalTable: "ServiceVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBookings_Services_ServiceId",
                table: "ServiceBookings",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 5) Remove migrated service rows from Products
            migrationBuilder.Sql("""
                DELETE FROM "ProductTags" pt
                USING "Products" p
                WHERE pt."ProductId" = p."Id" AND p."ProductType" = 1;

                DELETE FROM "ProductTranslations" t
                USING "Products" p
                WHERE t."ProductId" = p."Id" AND p."ProductType" = 1;

                DELETE FROM "ProductVariants" v
                USING "Products" p
                WHERE v."ProductId" = p."Id" AND p."ProductType" = 1;

                DELETE FROM "Products" WHERE "ProductType" = 1;
                """);

            // 6) Drop type discriminator / service-only column from Products
            migrationBuilder.DropColumn(
                name: "HidePrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceBookings_ServiceVariants_ServiceVariantId",
                table: "ServiceBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceBookings_Services_ServiceId",
                table: "ServiceBookings");

            migrationBuilder.DropTable(
                name: "ServiceTranslations");

            migrationBuilder.DropTable(
                name: "ServiceVariants");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.RenameColumn(
                name: "ServiceVariantId",
                table: "ServiceBookings",
                newName: "ProductVariantId");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "ServiceBookings",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceBookings_ServiceVariantId",
                table: "ServiceBookings",
                newName: "IX_ServiceBookings_ProductVariantId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceBookings_ServiceId",
                table: "ServiceBookings",
                newName: "IX_ServiceBookings_ProductId");

            migrationBuilder.AddColumn<bool>(
                name: "HidePrice",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBookings_ProductVariants_ProductVariantId",
                table: "ServiceBookings",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceBookings_Products_ProductId",
                table: "ServiceBookings",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
