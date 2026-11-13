using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Grading;

public sealed record GradingSessionRow(Guid EnrollmentId, Guid? SessionId, string StudentName, string StudentEmail,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string State, decimal? FinalTotal, decimal? SuggestedTotal, decimal? LecturerTotal, decimal MaxTotal);
public sealed record GradingExamList(Guid ExamId, string ExamName, IReadOnlyList<GradingSessionRow> Sessions);
public sealed record EvidenceSignals(double AnswerSeconds, int TurnLimitSeconds, int FollowUpCount, int AudioSeconds, int AnswerLength);
public sealed record TurnEvidence(Guid TurnId, AnswerType AnswerType, int FollowUpLevel, string PromptText, string? Transcript,
    DateTimeOffset AskedAt, DateTimeOffset? AnsweredAt, bool TimedOut, double AnswerSeconds, int TurnLimitSeconds,
    int? AudioDurationSeconds, int AnswerLength, string? AudioUrl, string? AudioMimeType);
public sealed record CriterionReview(Guid Id, string Name, string? Description, string? ExpectedConcepts, decimal MaxScore,
    int DisplayOrder, decimal? SuggestedScore, string? Justification, decimal? LecturerScore);
public sealed record QuestionReview(Guid SessionQuestionId, int Position, string QuestionText, string? ExpectedAnswer,
    IReadOnlyList<CriterionReview> Criteria, IReadOnlyList<TurnEvidence> Turns, EvidenceSignals Signals,
    string SuggestionState, string? SuggestionError, string? AiModel, DateTimeOffset? SuggestedAt,
    string? Strengths, string? Weaknesses, string? MissingPoints, decimal? SuggestedTotal, decimal? LecturerTotal,
    decimal MaxScore, string? LecturerComment);
public sealed record SessionReview(Guid SessionId, Guid ExamId, string ExamName, string StudentName, string StudentEmail,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string State, string Version, bool CanEdit, bool CanReopen,
    Guid? PreviousSessionId, Guid? NextSessionId, IReadOnlyList<QuestionReview> Questions, string? OverallComment,
    decimal? SuggestedTotal, decimal? LecturerTotal, decimal MaxTotal, DateTimeOffset? FinalisedAt,
    string? FinalisedByName, EvidenceSignals Signals);
public sealed record StudentQuestionResult(int Position, string QuestionText, decimal Score, decimal MaxScore, string? Comment);
public sealed record StudentFinalResult(decimal Total, decimal MaxTotal, DateTimeOffset FinalisedAt,
    string? OverallComment, IReadOnlyList<StudentQuestionResult> Questions);

public sealed class CriterionGradeInput { public Guid CriterionId { get; set; } public decimal? Score { get; set; } }
public sealed class QuestionGradeInput
{
    public Guid SessionQuestionId { get; set; }
    public string? Comment { get; set; }
    public List<CriterionGradeInput> Criteria { get; set; } = [];
}
public sealed class GradeInput
{
    public string Version { get; set; } = "";
    public string? OverallComment { get; set; }
    public List<QuestionGradeInput> Questions { get; set; } = [];
}
public sealed class GradingValidationException(IReadOnlyDictionary<string,string> errors) : Exception("Kiểm tra các điểm được đánh dấu.")
{ public IReadOnlyDictionary<string,string> Errors { get; } = errors; }

public sealed record GradingCriterion(Guid Id, string Name, string? Description, string? ExpectedConcepts, decimal MaxScore, int DisplayOrder);
public sealed record GradeConversation(string Prompt, string? Transcript);
// Deliberately contains no time, audio-duration, length or other secondary signals.
public sealed record QuestionGradingContext(string Question, string? ExpectedAnswer, IReadOnlyList<GradingCriterion> Criteria,
    IReadOnlyList<GradeConversation> Conversation);
public sealed record SuggestedCriterion(Guid CriterionId, decimal Score, string Justification);
public sealed record QuestionSuggestion(IReadOnlyList<SuggestedCriterion> Criteria, string Strengths, string Weaknesses,
    string MissingPoints, string ModelName);
public sealed record GradingAudio(Guid ExamId, string FilePath, string MimeType);
public interface IAiRubricGrader
{ Task<QuestionSuggestion> SuggestAsync(QuestionGradingContext context, CancellationToken ct); }
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
