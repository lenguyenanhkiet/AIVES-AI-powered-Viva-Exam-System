using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIpoweredVivaExamSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVivaInterviewCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentAnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnswerType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FollowUpLevel = table.Column<int>(type: "int", nullable: false),
                    PromptText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Transcript = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    AskedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAnswers", x => x.Id);
                    table.CheckConstraint("CK_StudentAnswers_AnsweredAt", "[AnsweredAt] IS NULL OR [AnsweredAt] >= [AskedAt]");
                    table.CheckConstraint("CK_StudentAnswers_FollowUpLevel", "[FollowUpLevel] >= 0");
                    table.ForeignKey(
                        name: "FK_StudentAnswers_SessionQuestions_SessionQuestionId",
                        column: x => x.SessionQuestionId,
                        principalTable: "SessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAnswers_StudentAnswers_ParentAnswerId",
                        column: x => x.ParentAnswerId,
                        principalTable: "StudentAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnswerAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentAnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    MissingConcepts = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AnalysisSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FollowUpPrompt = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnswerAnalyses_StudentAnswers_StudentAnswerId",
                        column: x => x.StudentAnswerId,
                        principalTable: "StudentAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnswerAudios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentAnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerAudios", x => x.Id);
                    table.CheckConstraint("CK_AnswerAudios_Duration", "[DurationSeconds] IS NULL OR [DurationSeconds] >= 0");
                    table.CheckConstraint("CK_AnswerAudios_FileSize", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 20971520");
                    table.ForeignKey(
                        name: "FK_AnswerAudios_StudentAnswers_StudentAnswerId",
                        column: x => x.StudentAnswerId,
                        principalTable: "StudentAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerAnalyses_StudentAnswerId",
                table: "AnswerAnalyses",
                column: "StudentAnswerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnswerAudios_StudentAnswerId",
                table: "AnswerAudios",
                column: "StudentAnswerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnswers_ParentAnswerId",
                table: "StudentAnswers",
                column: "ParentAnswerId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnswers_SessionQuestionId_FollowUpLevel",
                table: "StudentAnswers",
                columns: new[] { "SessionQuestionId", "FollowUpLevel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerAnalyses");

            migrationBuilder.DropTable(
                name: "AnswerAudios");

            migrationBuilder.DropTable(
                name: "StudentAnswers");
        }
    }
}

