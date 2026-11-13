using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record StudentQuestionResult(int Position, string QuestionText, decimal Score, decimal MaxScore, string? Comment);
