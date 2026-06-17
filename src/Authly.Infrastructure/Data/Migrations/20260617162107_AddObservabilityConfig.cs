using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddObservabilityConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observability_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    exporter = table.Column<string>(type: "text", nullable: false, defaultValue: "otlp"),
                    otlp_endpoint = table.Column<string>(type: "text", nullable: true),
                    otlp_headers_encrypted = table.Column<string>(type: "text", nullable: true),
                    azure_connection_string_encrypted = table.Column<string>(type: "text", nullable: true),
                    signals = table.Column<string>(type: "text", nullable: false, defaultValue: "traces,metrics,logs"),
                    sampling_ratio = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.0),
                    log_stream_endpoint = table.Column<string>(type: "text", nullable: true),
                    log_stream_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observability_config", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observability_config");
        }
    }
}
