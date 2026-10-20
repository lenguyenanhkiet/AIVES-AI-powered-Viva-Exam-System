using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Exams;

public sealed record ExamPoolQuestion(Guid Id, string Content, BloomLevel BloomLevel,
    QuestionDifficulty Difficulty, QuestionStatus Status, bool HasRubric, bool IsDeleted)
{
    public bool IsEligible => !IsDeleted && Status == QuestionStatus.Approved && HasRubric;
}

public sealed record ExamPoolPage(ExamSummary Exam, int RequiredQuestions, int CurrentPoolCount,
    int EligiblePoolCount, IReadOnlyList<ExamPoolQuestion> AvailableQuestions,
    IReadOnlyList<ExamPoolQuestion> PooledQuestions);

public interface IExamPoolReader
{
    // Counts only pool entries that remain eligible for publishing or a new session.
    Task<int> CountEligiblePoolQuestionsAsync(Guid examId, CancellationToken ct);
}

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
