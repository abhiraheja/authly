using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSuperAdminAndCloudTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "self_hosted_instances");

            migrationBuilder.DropTable(
                name: "super_admins");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    severity = table.Column<string>(type: "text", nullable: false, defaultValue: "info"),
                    title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "self_hosted_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    app_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    health = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    owner_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sync_key_hash = table.Column<string>(type: "text", nullable: false),
                    tenant_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    user_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    version = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_self_hosted_instances", x => x.id);
                    table.ForeignKey(
                        name: "FK_self_hosted_instances_tenants_owner_tenant_id",
                        column: x => x.owner_tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "super_admins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    email = table.Column<string>(type: "text", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mfa_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false, defaultValue: "Operator")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_super_admins", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_announcements_visible",
                table: "announcements",
                columns: new[] { "is_active", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "idx_self_hosted_instances_sync_key",
                table: "self_hosted_instances",
                column: "sync_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_self_hosted_instances_owner_tenant_id",
                table: "self_hosted_instances",
                column: "owner_tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_super_admins_email",
                table: "super_admins",
                column: "email",
                unique: true);
        }
    }
}
