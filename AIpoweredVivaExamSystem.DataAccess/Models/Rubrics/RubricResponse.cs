namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed record RubricResponse(Guid Id, Guid QuestionId, string Name, string? Description,
    decimal TotalScore, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    IReadOnlyList<RubricCriterionResponse> Criteria);
