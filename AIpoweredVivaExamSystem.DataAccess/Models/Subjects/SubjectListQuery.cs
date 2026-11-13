using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Subjects;

public sealed class SubjectListQuery
{
    // Matches Code or Name.
    public string? Keyword { get; init; }
    public AcademicStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
