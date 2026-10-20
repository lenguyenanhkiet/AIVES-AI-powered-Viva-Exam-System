using AIpoweredVivaExamSystem.Application.Exams;

namespace AIpoweredVivaExamSystem.Application.ExamStudents;

public sealed record RosterExamPage(IReadOnlyList<ExamSummary> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Guarded roster-page metadata without depending on the exam CRUD module or a capped list.</summary>
public interface IExamRosterReader
{
    Task<RosterExamPage> ListManagedAsync(int page, CancellationToken ct);
    Task<ExamSummary> GetExamAsync(Guid examId, CancellationToken ct);
}
