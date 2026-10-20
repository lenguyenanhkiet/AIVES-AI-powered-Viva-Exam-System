using AIpoweredVivaExamSystem.Application.Exams;

namespace AIpoweredVivaExamSystem.Application.ExamStudents;

/// <summary>Guarded roster-page metadata without depending on the exam CRUD module or a capped list.</summary>
public interface IExamRosterReader
{
    Task<ExamSummary> GetExamAsync(Guid examId, CancellationToken ct);
}
