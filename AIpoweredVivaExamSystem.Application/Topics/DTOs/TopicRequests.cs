using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Topics.DTOs;

public sealed record CreateTopicRequest(
    Guid SubjectId, string Name, string? Description, AcademicStatus Status = AcademicStatus.Active);

// SubjectId is fixed after creation.
public sealed record UpdateTopicRequest(string Name, string? Description, AcademicStatus Status);

public sealed class TopicListQuery
{
    public Guid? SubjectId { get; init; }
    public string? Keyword { get; init; }
    public AcademicStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
