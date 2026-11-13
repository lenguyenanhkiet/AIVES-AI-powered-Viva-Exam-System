using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record StudentFinalResult(decimal Total, decimal MaxTotal, DateTimeOffset FinalisedAt,
    string? OverallComment, IReadOnlyList<StudentQuestionResult> Questions);
