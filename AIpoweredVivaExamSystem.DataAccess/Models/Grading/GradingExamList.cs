using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record GradingExamList(Guid ExamId, string ExamName, IReadOnlyList<GradingSessionRow> Sessions);
