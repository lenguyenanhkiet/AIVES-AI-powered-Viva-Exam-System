namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed record CriterionRequest(
    string Name, string? Description, string? ExpectedConcepts,
    decimal MaxScore, int DisplayOrder, Guid? Id = null);
