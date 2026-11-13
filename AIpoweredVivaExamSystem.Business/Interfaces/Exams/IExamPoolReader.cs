using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IExamPoolReader
{
    // Counts only pool entries that remain eligible for publishing or a new session.
    Task<int> CountEligiblePoolQuestionsAsync(Guid examId, CancellationToken ct);
}
