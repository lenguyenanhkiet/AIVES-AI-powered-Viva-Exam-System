using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Subjects;

public sealed record UpdateSubjectRequest(
    string Code, string Name, string? Description, AcademicStatus Status);
