using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;

public interface IExamRepository
{
    Task<Exam?> FindByIdAsync(Guid examId, CancellationToken ct);
    Task<Exam> LockAsync(Guid examId, CancellationToken ct);
    void Add(Exam exam);
    void Remove(Exam exam);
}
