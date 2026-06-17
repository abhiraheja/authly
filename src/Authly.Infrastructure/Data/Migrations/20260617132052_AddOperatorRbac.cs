using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operator_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operator_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operator_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operator_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "member_roles",
                columns: table => new
                {
                    organization_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_roles", x => new { x.organization_membership_id, x.operator_role_id });
                    table.ForeignKey(
                        name: "FK_member_roles_operator_roles_operator_role_id",
                        column: x => x.operator_role_id,
                        principalTable: "operator_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_roles_organization_memberships_organization_membersh~",
                        column: x => x.organization_membership_id,
                        principalTable: "organization_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operator_role_permissions",
                columns: table => new
                {
                    operator_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operator_role_permissions", x => new { x.operator_role_id, x.operator_permission_id });
                    table.ForeignKey(
                        name: "FK_operator_role_permissions_operator_permissions_operator_per~",
                        column: x => x.operator_permission_id,
                        principalTable: "operator_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_operator_role_permissions_operator_roles_operator_role_id",
                        column: x => x.operator_role_id,
                        principalTable: "operator_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_member_roles_org",
                table: "member_roles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_roles_operator_role_id",
                table: "member_roles",
                column: "operator_role_id");

            migrationBuilder.CreateIndex(
                name: "idx_operator_permissions_org_res_act",
                table: "operator_permissions",
                columns: new[] { "organization_id", "resource", "action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operator_role_permissions_operator_permission_id",
                table: "operator_role_permissions",
                column: "operator_permission_id");

            migrationBuilder.CreateIndex(
                name: "idx_operator_roles_org_name",
                table: "operator_roles",
                columns: new[] { "organization_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_roles");

            migrationBuilder.DropTable(
                name: "operator_role_permissions");

            migrationBuilder.DropTable(
                name: "operator_permissions");

            migrationBuilder.DropTable(
                name: "operator_roles");
        }
    }
}
