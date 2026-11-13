using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIpoweredVivaExamSystem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturerGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionGradings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiSuggestionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SuggestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SuggestionAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuggestionStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SuggestionError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LecturerScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LecturerComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionGradings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionGradings_SessionQuestions_SessionQuestionId",
                        column: x => x.SessionQuestionId,
                        principalTable: "SessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionGradings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VivaSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsFinalised = table.Column<bool>(type: "bit", nullable: false),
                    EditedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalisedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalisedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReopenedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionGradings", x => x.Id);
                    table.CheckConstraint("CK_SessionGradings_Finalised", "[IsFinalised] = 0 OR ([FinalisedBy] IS NOT NULL AND [FinalisedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SessionGradings_Users_FinalisedBy",
                        column: x => x.FinalisedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionGradings_VivaSessions_VivaSessionId",
                        column: x => x.VivaSessionId,
                        principalTable: "VivaSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionGradings_SessionQuestionId",
                table: "QuestionGradings",
                column: "SessionQuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionGradings_FinalisedBy",
                table: "SessionGradings",
                column: "FinalisedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SessionGradings_VivaSessionId",
                table: "SessionGradings",
                column: "VivaSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionGradings");

            migrationBuilder.DropTable(
                name: "SessionGradings");
        }
    }
}
