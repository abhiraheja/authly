using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false, defaultValue: "info"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_announcements_visible",
                table: "announcements",
                columns: new[] { "is_active", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");
        }
    }
}
