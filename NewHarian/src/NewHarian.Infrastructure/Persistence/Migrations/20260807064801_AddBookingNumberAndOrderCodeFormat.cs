using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingNumberAndOrderCodeFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "BookingNumber",
                table: "ServiceBookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ServiceBookings"
                SET "BookingNumber" = 'HAR-SERVICE-' || LPAD("Id"::text, 4, '0');
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Orders"
                SET "OrderNumber" = 'HAR-ORDER-' || LPAD("Id"::text, 4, '0');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BookingNumber",
                table: "ServiceBookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBookings_BookingNumber",
                table: "ServiceBookings",
                column: "BookingNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceBookings_BookingNumber",
                table: "ServiceBookings");

            migrationBuilder.DropColumn(
                name: "BookingNumber",
                table: "ServiceBookings");

            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}
