using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Subjects.DTOs;

public sealed record CreateSubjectRequest(
    string Code, string Name, string? Description, AcademicStatus Status = AcademicStatus.Active);

public sealed record UpdateSubjectRequest(
    string Code, string Name, string? Description, AcademicStatus Status);

public sealed class SubjectListQuery
{
    // Matches Code or Name.
    public string? Keyword { get; init; }
    public AcademicStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
