using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    effect = table.Column<string>(type: "text", nullable: false, defaultValue: "allow"),
                    action = table.Column<string>(type: "text", nullable: false, defaultValue: "*"),
                    resource_type = table.Column<string>(type: "text", nullable: false, defaultValue: "*"),
                    conditions = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'"),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_policies_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_access_policies_tenant",
                table: "access_policies",
                columns: new[] { "tenant_id", "enabled" });

            // Row Level Security backstop on access_policies (tenant-scoped), mirroring users/etc.
            migrationBuilder.Sql("ALTER TABLE access_policies ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE access_policies FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation ON access_policies
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON access_policies;");
            migrationBuilder.Sql("ALTER TABLE access_policies NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE access_policies DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "access_policies");
        }
    }
}
