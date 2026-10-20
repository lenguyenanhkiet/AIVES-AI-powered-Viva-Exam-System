using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Exams;

public interface IExamRepository
{
    Task<Exam?> FindByIdAsync(Guid examId, CancellationToken ct);
    Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct);
    void Add(Exam exam);
    void Remove(Exam exam);
}
