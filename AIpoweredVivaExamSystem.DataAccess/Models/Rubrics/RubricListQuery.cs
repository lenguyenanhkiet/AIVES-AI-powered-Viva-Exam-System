namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed class RubricListQuery
{
    public Guid? QuestionId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
