using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVietnamAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommuneCode",
                table: "ServiceBookings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneName",
                table: "ServiceBookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "ServiceBookings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceName",
                table: "ServiceBookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingCommuneCode",
                table: "Orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneCode",
                table: "JobApplications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "JobApplications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneCode",
                table: "Inquiries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneName",
                table: "Inquiries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "Inquiries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceName",
                table: "Inquiries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneCode",
                table: "Dealers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommuneName",
                table: "Dealers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "Dealers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceName",
                table: "Dealers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommuneCode",
                table: "ServiceBookings");

            migrationBuilder.DropColumn(
                name: "CommuneName",
                table: "ServiceBookings");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "ServiceBookings");

            migrationBuilder.DropColumn(
                name: "ProvinceName",
                table: "ServiceBookings");

            migrationBuilder.DropColumn(
                name: "ShippingCommuneCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CommuneCode",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CommuneCode",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "CommuneName",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "ProvinceName",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "CommuneCode",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "CommuneName",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "ProvinceName",
                table: "Dealers");
        }
    }
}
