using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceAndSelfHostSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    version = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_consent_records_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_consent_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "self_hosted_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    sync_key_hash = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    app_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    health = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
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

            migrationBuilder.CreateIndex(
                name: "idx_consent_records_user",
                table: "consent_records",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_consent_records_user_id",
                table: "consent_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_self_hosted_instances_sync_key",
                table: "self_hosted_instances",
                column: "sync_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_self_hosted_instances_owner_tenant_id",
                table: "self_hosted_instances",
                column: "owner_tenant_id");

            // Row Level Security backstop on consent_records (tenant-scoped), mirroring users/etc.
            // self_hosted_instances is platform-level (cloud control plane) — NOT tenant-scoped,
            // like super_admins, so it gets no RLS policy.
            migrationBuilder.Sql("ALTER TABLE consent_records ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE consent_records FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation ON consent_records
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON consent_records;");
            migrationBuilder.Sql("ALTER TABLE consent_records NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE consent_records DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "self_hosted_instances");
        }
    }
}
