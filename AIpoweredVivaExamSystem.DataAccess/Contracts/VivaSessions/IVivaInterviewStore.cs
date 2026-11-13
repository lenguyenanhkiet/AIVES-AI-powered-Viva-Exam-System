using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;

public interface IVivaInterviewStore
{
    Task<IAsyncDisposable> AcquireSubmissionAsync(Guid sessionId, CancellationToken ct);
    Task<VivaInterviewState> GetStateAsync(Guid sessionId, CancellationToken ct);
    Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, DateTimeOffset receivedAt, ExaminerDecision decision, CancellationToken ct);
    Task<ExaminerEvaluationContext> ReadEvaluationContextAsync(SubmitAnswerRequest request, CancellationToken ct);
    Task<string> GetSubjectHintAsync(Guid sessionId, CancellationToken ct);
}
