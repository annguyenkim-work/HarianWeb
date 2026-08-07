using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<short>(type: "smallint", nullable: false),
                    Module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLogEntries_CreatedAtUtc",
                table: "AppLogEntries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogEntries_Level_CreatedAtUtc",
                table: "AppLogEntries",
                columns: new[] { "Level", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLogEntries_Module_CreatedAtUtc",
                table: "AppLogEntries",
                columns: new[] { "Module", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLogEntries");
        }
    }
}
