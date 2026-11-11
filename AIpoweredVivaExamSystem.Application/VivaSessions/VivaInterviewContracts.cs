using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public sealed record VivaTurnItem(
    Guid TurnId,
    Guid SessionQuestionId,
    int MainPosition,
    AnswerType AnswerType,
    int FollowUpLevel,
    string PromptText,
    string? Transcript,
    DateTimeOffset AskedAt,
    DateTimeOffset? AnsweredAt,
    bool HasAudio);

public sealed record VivaInterviewState(
    Guid SessionId,
    Guid EnrollmentId,
    string ExamName,
    string SubjectCode,
    string SubjectName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset SlotEndAt,
    int RemainingSlotSeconds,
    int TurnLimitSeconds,
    int RemainingTurnSeconds,
    int CurrentMainPosition,
    int TotalMainQuestions,
    int CurrentFollowUpLevel,
    int MaxFollowUpPerQuestion,
    VivaTurnItem? ActiveTurn,
    IReadOnlyList<VivaTurnItem> Turns,
    IReadOnlyList<StudentQuestion> Questions);

public sealed record ConversationTurn(
    AnswerType AnswerType,
    int FollowUpLevel,
    string PromptText,
    string Transcript);

public sealed record ExaminerEvaluationContext(
    string SubjectName,
    string MainQuestionContent,
    string RubricSnapshotJson,
    int CurrentFollowUpLevel,
    int MaxFollowUpPerQuestion,
    IReadOnlyList<ConversationTurn> History);

public sealed record ExaminerDecision(
    bool IsComplete,
    string? MissingConcepts,
    string? AnalysisSummary,
    string? FollowUpPrompt,
    string ModelName);

public interface IVivaAiExaminer
{
    Task<ExaminerDecision> EvaluateAndFollowUpAsync(ExaminerEvaluationContext context, CancellationToken ct);
}

public interface ISpeechTranscriber
{
    Task<string> TranscribeAsync(byte[] audioBytes, string fileName, string language, string? promptHint, CancellationToken ct);
}

public interface IAnswerAudioStorage
{
    Task<string> SaveAudioAsync(Guid sessionId, Guid turnId, byte[] content, string mimeType, CancellationToken ct);
}

public sealed record SubmitAnswerRequest(
    Guid SessionId,
    Guid TurnId,
    string? Transcript,
    byte[]? AudioBytes = null,
    string? AudioMimeType = null,
    int? AudioDurationSeconds = null,
    string Language = "vi");

public interface IVivaInterviewStore
{
    Task<VivaInterviewState> GetStateAsync(Guid sessionId, CancellationToken ct);
    Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct);
    Task<string> GetSubjectHintAsync(Guid sessionId, CancellationToken ct);
}

public interface IVivaInterviewService
{
    Task<VivaInterviewState> OpenOrResumeByEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<VivaInterviewState> GetStateAsync(Guid sessionId, CancellationToken ct);
    Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct);
    Task<string> TranscribeAudioAsync(Guid sessionId, byte[] audioBytes, string fileName, string language, CancellationToken ct);
}

