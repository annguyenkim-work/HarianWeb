using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingPostalCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Fax",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "Furigana",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ColorDefinitionTranslations");

            migrationBuilder.Sql(
                """DELETE FROM "SiteSettings" WHERE "Key" = 'company.header_bg';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingPostalCode",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fax",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Furigana",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ColorDefinitionTranslations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
