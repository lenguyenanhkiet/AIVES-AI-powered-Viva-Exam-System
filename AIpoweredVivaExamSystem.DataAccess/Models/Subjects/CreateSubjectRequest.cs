using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Subjects;

public sealed record CreateSubjectRequest(
    string Code, string Name, string? Description, AcademicStatus Status = AcademicStatus.Active);
