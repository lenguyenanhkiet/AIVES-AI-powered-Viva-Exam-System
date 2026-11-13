using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;

public interface IExamFoundationReader
{
    Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct);
    Task<ExamFoundationCounts> CountsAsync(Guid examId, CancellationToken ct);
}
