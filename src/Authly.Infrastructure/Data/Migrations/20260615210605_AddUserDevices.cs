using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    last_ip = table.Column<string>(type: "text", nullable: true),
                    trusted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_devices_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_devices_fingerprint",
                table: "user_devices",
                columns: new[] { "tenant_id", "user_id", "fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_devices_user_id",
                table: "user_devices",
                column: "user_id");

            // Row Level Security backstop on user_devices (tenant-scoped), mirroring users/etc.
            migrationBuilder.Sql("ALTER TABLE user_devices ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE user_devices FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation ON user_devices
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON user_devices;");
            migrationBuilder.Sql("ALTER TABLE user_devices NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE user_devices DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "user_devices");
        }
    }
}
