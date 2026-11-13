using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Topics;

public sealed record TopicResponse(Guid Id, Guid SubjectId, string Name, string? Description,
    AcademicStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
