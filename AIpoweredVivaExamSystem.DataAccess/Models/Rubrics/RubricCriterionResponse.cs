namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed record RubricCriterionResponse(Guid Id, string Name, string? Description,
    string? ExpectedConcepts, decimal MaxScore, int DisplayOrder);
