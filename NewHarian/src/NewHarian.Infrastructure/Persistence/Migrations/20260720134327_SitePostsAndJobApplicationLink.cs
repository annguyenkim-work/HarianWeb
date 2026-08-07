using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SitePostsAndJobApplicationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SitePostId",
                table: "JobApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SitePosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CoverImageMediaFileId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitePosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitePosts_MediaFiles_CoverImageMediaFileId",
                        column: x => x.CoverImageMediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SitePostTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SitePostId = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitePostTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitePostTranslations_SitePosts_SitePostId",
                        column: x => x.SitePostId,
                        principalTable: "SitePosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_SitePostId",
                table: "JobApplications",
                column: "SitePostId");

            migrationBuilder.CreateIndex(
                name: "IX_SitePosts_CoverImageMediaFileId",
                table: "SitePosts",
                column: "CoverImageMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SitePosts_Kind_IsPublished_PublishedAt",
                table: "SitePosts",
                columns: new[] { "Kind", "IsPublished", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SitePosts_Kind_Slug",
                table: "SitePosts",
                columns: new[] { "Kind", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitePostTranslations_SitePostId_LanguageCode",
                table: "SitePostTranslations",
                columns: new[] { "SitePostId", "LanguageCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_JobApplications_SitePosts_SitePostId",
                table: "JobApplications",
                column: "SitePostId",
                principalTable: "SitePosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobApplications_SitePosts_SitePostId",
                table: "JobApplications");

            migrationBuilder.DropTable(
                name: "SitePostTranslations");

            migrationBuilder.DropTable(
                name: "SitePosts");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_SitePostId",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "SitePostId",
                table: "JobApplications");
        }
    }
}
