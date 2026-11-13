using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record SuggestedCriterion(Guid CriterionId, decimal Score, string Justification);
