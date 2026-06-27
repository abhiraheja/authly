using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveysEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surveys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    enforcement_mode = table.Column<string>(type: "text", nullable: false, defaultValue: "Optional"),
                    skip_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    targeting = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    randomize_questions = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    anonymous = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    show_progress_bar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    thank_you_message = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consent_reset_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surveys", x => x.id);
                    table.ForeignKey(
                        name: "FK_surveys_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    help_text = table.Column<string>(type: "text", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    media_url = table.Column<string>(type: "text", nullable: true),
                    scale_min = table.Column<int>(type: "integer", nullable: true),
                    scale_max = table.Column<int>(type: "integer", nullable: true),
                    randomize_options = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    placeholder = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_survey_questions_surveys_survey_id",
                        column: x => x.survey_id,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_survey_responses_surveys_survey_id",
                        column: x => x.survey_id,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_question_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_question_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_survey_question_options_survey_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "survey_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    response_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_value = table.Column<string>(type: "text", nullable: true),
                    number_value = table.Column<double>(type: "double precision", nullable: true),
                    option_ids = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_survey_answers_survey_responses_response_id",
                        column: x => x.response_id,
                        principalTable: "survey_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_survey_answers_response",
                table: "survey_answers",
                column: "response_id");

            migrationBuilder.CreateIndex(
                name: "idx_survey_options_question",
                table: "survey_question_options",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "idx_survey_questions_survey",
                table: "survey_questions",
                column: "survey_id");

            migrationBuilder.CreateIndex(
                name: "idx_survey_responses_user",
                table: "survey_responses",
                columns: new[] { "tenant_id", "survey_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_responses_survey_id",
                table: "survey_responses",
                column: "survey_id");

            migrationBuilder.CreateIndex(
                name: "idx_surveys_tenant_status",
                table: "surveys",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_answers");

            migrationBuilder.DropTable(
                name: "survey_question_options");

            migrationBuilder.DropTable(
                name: "survey_responses");

            migrationBuilder.DropTable(
                name: "survey_questions");

            migrationBuilder.DropTable(
                name: "surveys");
        }
    }
}
