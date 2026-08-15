using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Services_Status_CategoryId_SortOrder",
                table: "Services",
                columns: new[] { "Status", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Services_Status_IsFeatured",
                table: "Services",
                columns: new[] { "Status", "IsFeatured" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBookings_CreatedAt",
                table: "ServiceBookings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_CategoryId_SortOrder",
                table: "Products",
                columns: new[] { "Status", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_IsFeatured",
                table: "Products",
                columns: new[] { "Status", "IsFeatured" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_IsPrivate_CreatedAt",
                table: "MediaFiles",
                columns: new[] { "IsPrivate", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_CreatedAt",
                table: "JobApplications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Status",
                table: "JobApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HomeSlides_IsActive_SortOrder",
                table: "HomeSlides",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive_ShowOnHome_SortOrder",
                table: "Categories",
                columns: new[] { "IsActive", "ShowOnHome", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Services_Status_CategoryId_SortOrder",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_Status_IsFeatured",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_ServiceBookings_CreatedAt",
                table: "ServiceBookings");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status_CategoryId_SortOrder",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status_IsFeatured",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_IsPrivate_CreatedAt",
                table: "MediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_CreatedAt",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_Status",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_HomeSlides_IsActive_SortOrder",
                table: "HomeSlides");

            migrationBuilder.DropIndex(
                name: "IX_Categories_IsActive_ShowOnHome_SortOrder",
                table: "Categories");
        }
    }
}
