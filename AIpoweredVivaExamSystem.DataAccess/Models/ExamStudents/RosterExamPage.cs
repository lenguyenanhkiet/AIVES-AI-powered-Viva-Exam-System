using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;

namespace AIpoweredVivaExamSystem.DataAccess.Models.ExamStudents;

public sealed record RosterExamPage(IReadOnlyList<ExamSummary> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Guarded roster-page metadata without depending on the exam CRUD module or a capped list.</summary>
