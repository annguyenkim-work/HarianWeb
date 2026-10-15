using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DealerBusinessProfileAndDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Dealers");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceAddress",
                table: "Orders",
                type: "character varying(700)",
                maxLength: 700,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceBuyerName",
                table: "Orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceEmail",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceTaxCode",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "OrderItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "OrderItems",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountSource",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscountTierLabel",
                table: "OrderItems",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessLicenseMediaFileId",
                table: "Dealers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                table: "Dealers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BusinessType",
                table: "Dealers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HouseholdRegistrationNo",
                table: "Dealers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceEmail",
                table: "Dealers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "Dealers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeTitle",
                table: "Dealers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShipToHeadOffice",
                table: "Dealers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "Dealers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingCommuneCode",
                table: "Dealers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingCommuneName",
                table: "Dealers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingProvinceCode",
                table: "Dealers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingProvinceName",
                table: "Dealers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "Dealers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Dealers"
                SET "BusinessType" = 2,
                    "BusinessName" = "FullName",
                    "RepresentativeName" = "FullName",
                    "TaxCode" = 'LEGACY-' || "Id",
                    "ShipToHeadOffice" = TRUE
                WHERE "TaxCode" = '';
                """);

            migrationBuilder.CreateTable(
                name: "DealerDiscountGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerDiscountGroups", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "DealerDiscountTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerDiscountTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerDiscountTiers_DealerDiscountGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DealerDiscountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealerDiscountOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    TierId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerDiscountOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerDiscountOverrides_DealerDiscountTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "DealerDiscountTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealerDiscountOverrides_Dealers_DealerId",
                        column: x => x.DealerId,
                        principalTable: "Dealers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealerDiscountOverrides_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealerDiscountRates",
                columns: table => new
                {
                    TierId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerDiscountRates", x => new { x.TierId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_DealerDiscountRates_DealerDiscountTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "DealerDiscountTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealerDiscountRates_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dealers_BusinessLicenseMediaFileId",
                table: "Dealers",
                column: "BusinessLicenseMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Dealers_TaxCode",
                table: "Dealers",
                column: "TaxCode",
                unique: true,
                filter: "\"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountGroupProducts_ProductId",
                table: "DealerDiscountGroupProducts",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountGroups_Name",
                table: "DealerDiscountGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountOverrides_DealerId_TierId_ProductId",
                table: "DealerDiscountOverrides",
                columns: new[] { "DealerId", "TierId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountOverrides_ProductId",
                table: "DealerDiscountOverrides",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountOverrides_TierId",
                table: "DealerDiscountOverrides",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountRates_ProductId",
                table: "DealerDiscountRates",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerDiscountTiers_GroupId",
                table: "DealerDiscountTiers",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dealers_MediaFiles_BusinessLicenseMediaFileId",
                table: "Dealers",
                column: "BusinessLicenseMediaFileId",
                principalTable: "MediaFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dealers_MediaFiles_BusinessLicenseMediaFileId",
                table: "Dealers");

            migrationBuilder.DropTable(
                name: "DealerDiscountGroupProducts");

            migrationBuilder.DropTable(
                name: "DealerDiscountOverrides");

            migrationBuilder.DropTable(
                name: "DealerDiscountRates");

            migrationBuilder.DropTable(
                name: "DealerDiscountTiers");

            migrationBuilder.DropTable(
                name: "DealerDiscountGroups");

            migrationBuilder.DropIndex(
                name: "IX_Dealers_BusinessLicenseMediaFileId",
                table: "Dealers");

            migrationBuilder.DropIndex(
                name: "IX_Dealers_TaxCode",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "InvoiceAddress",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceBuyerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceEmail",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceTaxCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountSource",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountTierLabel",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BusinessLicenseMediaFileId",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "BusinessName",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "HouseholdRegistrationNo",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "InvoiceEmail",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "RepresentativeTitle",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ShipToHeadOffice",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ShippingAddress",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ShippingCommuneCode",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ShippingCommuneName",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ShippingProvinceCode",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ShippingProvinceName",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Dealers");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Dealers",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }
    }
}
