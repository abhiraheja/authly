using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsAndAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "tenants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    owner_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settings = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_organizations_accounts_owner_account_id",
                        column: x => x.owner_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Invited"),
                    invited_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_memberships_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_tenants_organization",
                table: "tenants",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_accounts_email",
                table: "accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_org_memberships_account",
                table: "organization_memberships",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "idx_org_memberships_account_org",
                table: "organization_memberships",
                columns: new[] { "account_id", "organization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_organization_id",
                table: "organization_memberships",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_owner_account_id",
                table: "organizations",
                column: "owner_account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_organizations_organization_id",
                table: "tenants",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenants_organizations_organization_id",
                table: "tenants");

            migrationBuilder.DropTable(
                name: "organization_memberships");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropIndex(
                name: "idx_tenants_organization",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "tenants");
        }
    }
}
