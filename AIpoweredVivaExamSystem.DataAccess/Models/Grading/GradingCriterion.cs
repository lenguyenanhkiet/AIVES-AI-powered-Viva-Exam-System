using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record GradingCriterion(Guid Id, string Name, string? Description, string? ExpectedConcepts, decimal MaxScore, int DisplayOrder);
