using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewHarian.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Idempotent seed: SuperAdmin role + admin@harian.local / Admin@12345
    /// (Identity PasswordHasher V3 / PBKDF2). Complements DbSeeder for environments that only run migrations.
    /// </summary>
    public partial class SeedDefaultAdminUser : Migration
    {
        private const string AdminUserId = "a1111111-1111-1111-1111-111111111111";
        private const string SuperAdminRoleId = "a0000000-0000-0000-0000-000000000001";
        // PasswordHasher.HashPassword(..., "Admin@12345") — salt random; verify works with Identity
        private const string AdminPasswordHash =
            "AQAAAAIAAYagAAAAEOdNTb+8rejX8gR2r3EFpw/3SxKGi+aY5EIUyIamwth87ktiLdKedNSyS72sZXQdtQ==";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                SELECT '{SuperAdminRoleId}', 'SuperAdmin', 'SUPERADMIN', '{Guid.NewGuid():D}'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "AspNetRoles" WHERE "NormalizedName" = 'SUPERADMIN'
                );

                INSERT INTO "AspNetUsers" (
                    "Id", "FullName", "IsActive", "CreatedAt", "UpdatedAt",
                    "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
                    "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
                    "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount"
                )
                SELECT
                    '{AdminUserId}',
                    'System Admin',
                    TRUE,
                    TIMESTAMPTZ '2026-10-27 00:00:00+00',
                    NULL,
                    'admin@harian.local',
                    'ADMIN@HARIAN.LOCAL',
                    'admin@harian.local',
                    'ADMIN@HARIAN.LOCAL',
                    TRUE,
                    '{AdminPasswordHash}',
                    '{Guid.NewGuid():N}',
                    '{Guid.NewGuid():D}',
                    NULL,
                    FALSE,
                    FALSE,
                    NULL,
                    TRUE,
                    0
                WHERE NOT EXISTS (
                    SELECT 1 FROM "AspNetUsers" WHERE "NormalizedEmail" = 'ADMIN@HARIAN.LOCAL'
                );

                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT u."Id", r."Id"
                FROM "AspNetUsers" AS u
                CROSS JOIN "AspNetRoles" AS r
                WHERE u."NormalizedEmail" = 'ADMIN@HARIAN.LOCAL'
                  AND r."NormalizedName" = 'SUPERADMIN'
                  AND NOT EXISTS (
                      SELECT 1 FROM "AspNetUserRoles" AS ur
                      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM "AspNetUserRoles" AS ur
                USING "AspNetUsers" AS u
                WHERE ur."UserId" = u."Id"
                  AND u."Id" = '{AdminUserId}';

                DELETE FROM "AspNetUsers"
                WHERE "Id" = '{AdminUserId}';

                DELETE FROM "AspNetRoles"
                WHERE "Id" = '{SuperAdminRoleId}'
                  AND NOT EXISTS (
                      SELECT 1 FROM "AspNetUserRoles" WHERE "RoleId" = '{SuperAdminRoleId}'
                  );
                """);
        }
    }
}
