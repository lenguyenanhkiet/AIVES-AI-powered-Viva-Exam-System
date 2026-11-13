using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Topics;

public sealed record CreateTopicRequest(
    Guid SubjectId, string Name, string? Description, AcademicStatus Status = AcademicStatus.Active);

// SubjectId is fixed after creation.
