using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IExamService
{
    Task<ExamDetails> CreateAsync(ExamConfiguration request, CancellationToken ct);
    Task<ExamDetails> GetAsync(Guid examId, CancellationToken ct);
    Task UpdateAsync(Guid examId, ExamConfiguration request, byte[] expectedVersion, CancellationToken ct);
    Task TransitionAsync(Guid examId, ExamStatus target, byte[] expectedVersion, CancellationToken ct);
}
