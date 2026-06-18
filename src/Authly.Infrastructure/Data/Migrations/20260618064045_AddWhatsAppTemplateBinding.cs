using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppTemplateBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_language",
                table: "message_templates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_template_name",
                table: "message_templates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider_language",
                table: "message_templates");

            migrationBuilder.DropColumn(
                name: "provider_template_name",
                table: "message_templates");
        }
    }
}
