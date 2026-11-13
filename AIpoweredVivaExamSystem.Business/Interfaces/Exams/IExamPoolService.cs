using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IExamPoolService
{
    Task<IReadOnlyList<PoolQuestion>> ListEligibleAsync(Guid examId, CancellationToken ct);
    Task<int> CountEligibleAsync(Guid examId, CancellationToken ct);
    Task AddAsync(Guid examId, IReadOnlyCollection<Guid> questionIds, CancellationToken ct);
    Task RemoveAsync(Guid examId, Guid questionId, CancellationToken ct);
}
// Optional trailing Email preserves existing callers while allowing the roster UI to identify students.
