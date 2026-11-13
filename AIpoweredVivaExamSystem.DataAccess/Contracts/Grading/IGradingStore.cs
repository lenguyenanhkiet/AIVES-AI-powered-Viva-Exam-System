using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Grading;

public interface IGradingStore
{
    Task<Guid> ExamForSessionAsync(Guid sessionId, CancellationToken ct);
    Task<GradingExamList> ListAsync(Guid examId, CancellationToken ct);
    Task<SessionReview> ReviewAsync(Guid sessionId, CancellationToken ct);
    Task SaveAsync(Guid sessionId, GradeInput input, bool finalise, Guid lecturerId, CancellationToken ct);
    Task ReopenAsync(Guid sessionId, string version, Guid lecturerId, CancellationToken ct);
    Task<(Guid AttemptId, QuestionGradingContext Context)?> BeginSuggestionAsync(Guid sessionId, Guid questionId, bool regenerate, CancellationToken ct);
    Task FinishSuggestionAsync(Guid questionId, Guid attemptId, QuestionSuggestion? suggestion, string? error, CancellationToken ct);
    Task<GradingAudio> AudioAsync(Guid audioId, CancellationToken ct);
}
