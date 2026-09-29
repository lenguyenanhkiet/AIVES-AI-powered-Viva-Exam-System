using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Subjects.DTOs;

public sealed record SubjectResponse(Guid Id, string Code, string Name, string? Description,
    AcademicStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
