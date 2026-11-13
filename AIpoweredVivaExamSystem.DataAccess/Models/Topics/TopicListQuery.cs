using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Topics;

public sealed class TopicListQuery
{
    public Guid? SubjectId { get; init; }
    public string? Keyword { get; init; }
    public AcademicStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
