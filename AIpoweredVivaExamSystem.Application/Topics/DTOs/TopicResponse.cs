using AIpoweredVivaExamSystem.Application.Contracts;

namespace AIpoweredVivaExamSystem.Application.Topics.DTOs;

public sealed record TopicResponse(Guid Id, Guid SubjectId, string Name, string? Description,
    AcademicStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
