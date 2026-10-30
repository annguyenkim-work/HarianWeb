using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JobPositionId",
                table: "EmployeeProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeProfiles_JobPositionId",
                table: "EmployeeProfiles",
                column: "JobPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPositions_Name",
                table: "JobPositions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPositions_SortOrder",
                table: "JobPositions",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeProfiles_JobPositions_JobPositionId",
                table: "EmployeeProfiles",
                column: "JobPositionId",
                principalTable: "JobPositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeProfiles_JobPositions_JobPositionId",
                table: "EmployeeProfiles");

            migrationBuilder.DropTable(
                name: "JobPositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeProfiles_JobPositionId",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "JobPositionId",
                table: "EmployeeProfiles");
        }
    }
}
