using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIpoweredVivaExamSystem.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVivaAiGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiGradings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AiComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Strengths = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Weaknesses = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MissingPoints = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TotalResponseSeconds = table.Column<int>(type: "int", nullable: false),
                    WordsPerMinute = table.Column<int>(type: "int", nullable: false),
                    FluencyNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGradings", x => x.Id);
                    table.CheckConstraint("CK_AiGradings_AiScore", "[AiScore] >= 0 AND [AiScore] <= [MaxScore]");
                    table.CheckConstraint("CK_AiGradings_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
                    table.CheckConstraint("CK_AiGradings_ResponseSeconds", "[TotalResponseSeconds] >= 0");
                    table.CheckConstraint("CK_AiGradings_WordsPerMinute", "[WordsPerMinute] >= 0");
                    table.ForeignKey(
                        name: "FK_AiGradings_SessionQuestions_SessionQuestionId",
                        column: x => x.SessionQuestionId,
                        principalTable: "SessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiScoreSnapshot = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    TeacherScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherReviews", x => x.Id);
                    table.CheckConstraint("CK_TeacherReviews_FinalScore", "[FinalScore] >= 0 AND [FinalScore] <= [MaxScore]");
                    table.CheckConstraint("CK_TeacherReviews_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
                    table.CheckConstraint("CK_TeacherReviews_TeacherScore", "[TeacherScore] >= 0 AND [TeacherScore] <= [MaxScore]");
                    table.ForeignKey(
                        name: "FK_TeacherReviews_SessionQuestions_SessionQuestionId",
                        column: x => x.SessionQuestionId,
                        principalTable: "SessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherReviews_Users_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiGradingCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiGradingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricCriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriterionName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGradingCriteria", x => x.Id);
                    table.CheckConstraint("CK_AiGradingCriteria_DisplayOrder", "[DisplayOrder] >= 1");
                    table.CheckConstraint("CK_AiGradingCriteria_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
                    table.CheckConstraint("CK_AiGradingCriteria_Score", "[Score] >= 0 AND [Score] <= [MaxScore]");
                    table.ForeignKey(
                        name: "FK_AiGradingCriteria_AiGradings_AiGradingId",
                        column: x => x.AiGradingId,
                        principalTable: "AiGradings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiGradingCriteria_AiGradingId_DisplayOrder",
                table: "AiGradingCriteria",
                columns: new[] { "AiGradingId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGradings_SessionQuestionId",
                table: "AiGradings",
                column: "SessionQuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviews_LecturerId",
                table: "TeacherReviews",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviews_SessionQuestionId",
                table: "TeacherReviews",
                column: "SessionQuestionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiGradingCriteria");

            migrationBuilder.DropTable(
                name: "TeacherReviews");

            migrationBuilder.DropTable(
                name: "AiGradings");
        }
    }
}
