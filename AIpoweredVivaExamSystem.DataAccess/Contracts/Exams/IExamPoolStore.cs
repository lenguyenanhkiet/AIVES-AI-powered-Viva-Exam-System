using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;

public interface IExamPoolStore
{
    Task<Exam> LockExamAsync(Guid examId, CancellationToken ct);
    Task<ExamPoolPage?> ReadAsync(Guid examId, CancellationToken ct);
    Task<int> CountEligiblePoolQuestionsAsync(Guid examId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> FindEligibleQuestionIdsAsync(Exam exam, IReadOnlyCollection<Guid> questionIds, CancellationToken ct);
    Task<IReadOnlySet<Guid>> FindPooledQuestionIdsAsync(Guid examId, IReadOnlyCollection<Guid> questionIds, CancellationToken ct);
    Task<ExamQuestionPool[]> FindPoolEntriesAsync(Guid examId, IReadOnlyCollection<Guid> questionIds, CancellationToken ct);
    void Add(ExamQuestionPool entry);
    void RemoveRange(IEnumerable<ExamQuestionPool> entries);
}
