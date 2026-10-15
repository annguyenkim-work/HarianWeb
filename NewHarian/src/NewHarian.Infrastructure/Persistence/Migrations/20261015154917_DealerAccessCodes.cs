using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DealerAccessCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSensitive",
                table: "EmailOutboxMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccessCodeHash",
                table: "Dealers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessCodeIssuedAt",
                table: "Dealers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessCodeIssuedByUserId",
                table: "Dealers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessCodeLast4",
                table: "Dealers",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dealers_AccessCodeHash",
                table: "Dealers",
                column: "AccessCodeHash",
                unique: true,
                filter: "\"AccessCodeHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dealers_AccessCodeHash",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "IsSensitive",
                table: "EmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "AccessCodeHash",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "AccessCodeIssuedAt",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "AccessCodeIssuedByUserId",
                table: "Dealers");

            migrationBuilder.DropColumn(
                name: "AccessCodeLast4",
                table: "Dealers");
        }
    }
}
