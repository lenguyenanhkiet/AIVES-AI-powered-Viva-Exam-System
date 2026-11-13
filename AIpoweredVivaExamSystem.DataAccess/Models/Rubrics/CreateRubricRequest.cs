namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed record CreateRubricRequest(
    Guid QuestionId, string Name, string? Description, List<CriterionRequest> Criteria);

// PUT replaces the complete criterion list. Omitted existing criteria are removed.
