using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Subjects;

public sealed record SubjectResponse(Guid Id, string Code, string Name, string? Description,
    AcademicStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
