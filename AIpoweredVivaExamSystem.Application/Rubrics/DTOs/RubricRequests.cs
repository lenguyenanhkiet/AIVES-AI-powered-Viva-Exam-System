namespace AIpoweredVivaExamSystem.Application.Rubrics.DTOs;

public sealed record CriterionRequest(
    string Name, string? Description, string? ExpectedConcepts,
    decimal MaxScore, int DisplayOrder, Guid? Id = null);

public sealed record CreateRubricRequest(
    Guid QuestionId, string Name, string? Description, List<CriterionRequest> Criteria);

// PUT replaces the complete criterion list. Omitted existing criteria are removed.
public sealed record UpdateRubricRequest(
    string Name, string? Description, List<CriterionRequest> Criteria);

public sealed class RubricListQuery
{
    public Guid? QuestionId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
