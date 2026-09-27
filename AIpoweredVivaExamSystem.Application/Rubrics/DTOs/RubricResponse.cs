namespace AIpoweredVivaExamSystem.Application.Rubrics.DTOs;

public sealed record RubricCriterionResponse(Guid Id, string Name, string? Description,
    string? ExpectedConcepts, decimal MaxScore, int DisplayOrder);

public sealed record RubricResponse(Guid Id, Guid QuestionId, string Name, string? Description,
    decimal TotalScore, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    IReadOnlyList<RubricCriterionResponse> Criteria);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
