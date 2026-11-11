using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public enum VivaGradingStatus
{
    NotStarted,
    PendingAiGrading,
    AiSuggested,
    PartiallyReviewed,
    Reviewed
}

public sealed record RubricCriterionSnapshotItem(
    Guid? Id,
    string Name,
    string? Description,
    string? ExpectedConcepts,
    decimal MaxScore,
    int DisplayOrder);

public sealed record AiCriterionEvaluation(
    Guid? RubricCriterionId,
    string CriterionName,
    decimal MaxScore,
    decimal Score,
    string? Evidence,
    string? Comment,
    int DisplayOrder);

public sealed record AiGradingEvaluationContext(
    string SubjectName,
    string QuestionContent,
    string RubricSnapshotJson,
    decimal MaxScore,
    int TotalResponseSeconds,
    int WordsPerMinute,
    IReadOnlyList<ConversationTurn> History);

public sealed record AiGradingDecision(
    decimal AiScore,
    decimal MaxScore,
    string? AiComment,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    string? FluencyNote,
    string ModelName,
    IReadOnlyList<AiCriterionEvaluation> Criteria);

public interface IVivaAiGrader
{
    Task<AiGradingDecision> GradeQuestionAsync(AiGradingEvaluationContext context, CancellationToken ct);
}

public sealed record GradedTurnItem(
    Guid TurnId,
    AnswerType AnswerType,
    int FollowUpLevel,
    string PromptText,
    string? Transcript,
    DateTimeOffset AskedAt,
    DateTimeOffset? AnsweredAt,
    int? ResponseSeconds,
    int WordCount,
    bool HasAudio,
    string? MissingConceptsFromLiveAnalysis,
    string? LiveAnalysisSummary);

public sealed record AiGradingDetails(
    Guid AiGradingId,
    decimal AiScore,
    decimal MaxScore,
    string? AiComment,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    int TotalResponseSeconds,
    int WordsPerMinute,
    string? FluencyNote,
    string ModelName,
    DateTimeOffset GradedAt,
    IReadOnlyList<AiCriterionEvaluation> Criteria);

public sealed record TeacherReviewDetails(
    Guid ReviewId,
    Guid LecturerId,
    string LecturerName,
    decimal? AiScoreSnapshot,
    decimal TeacherScore,
    decimal FinalScore,
    decimal MaxScore,
    string? Comment,
    DateTimeOffset ReviewedAt);

public sealed record SessionQuestionGradingItem(
    Guid SessionQuestionId,
    Guid QuestionId,
    int Position,
    string QuestionContent,
    string? ExpectedAnswer,
    string RubricName,
    decimal MaxScore,
    IReadOnlyList<RubricCriterionSnapshotItem> RubricCriteria,
    IReadOnlyList<GradedTurnItem> Turns,
    int TotalResponseSeconds,
    int TotalWordCount,
    int WordsPerMinute,
    AiGradingDetails? AiGrading,
    TeacherReviewDetails? TeacherReview);

public sealed record VivaSessionGradingSheet(
    Guid SessionId,
    Guid EnrollmentId,
    Guid ExamId,
    string ExamName,
    string SubjectCode,
    string SubjectName,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    decimal TotalMaxScore,
    decimal? TotalAiScore,
    decimal? TotalFinalScore,
    decimal? NormalizedAiScore10,
    decimal? NormalizedFinalScore10,
    int AiGradedQuestionsCount,
    int TeacherReviewedQuestionsCount,
    int TotalQuestionsCount,
    IReadOnlyList<SessionQuestionGradingItem> Questions);

public sealed record ExamStudentGradingRow(
    Guid EnrollmentId,
    Guid? SessionId,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    DateTimeOffset? ScheduledAt,
    SessionState SessionStatus,
    VivaGradingStatus GradingStatus,
    int TotalQuestions,
    int AiGradedCount,
    int TeacherReviewedCount,
    decimal? TotalMaxScore,
    decimal? TotalAiScore,
    decimal? TotalFinalScore,
    decimal? NormalizedFinalScore10);

public sealed record ExamGradingOverview(
    ExamSummary Exam,
    string SubjectCode,
    string SubjectName,
    int TotalStudents,
    int CompletedSessionsCount,
    int FullyReviewedSessionsCount,
    IReadOnlyList<ExamStudentGradingRow> Students);

public sealed record StudentQuestionResultItem(
    int Position,
    string QuestionContent,
    decimal MaxScore,
    decimal? AiScore,
    string? AiComment,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    int TotalResponseSeconds,
    int WordsPerMinute,
    string? FluencyNote,
    decimal? FinalScore,
    string? TeacherComment,
    DateTimeOffset? ReviewedAt);

public sealed record StudentSessionGradingResult(
    Guid SessionId,
    decimal TotalMaxScore,
    decimal? TotalAiScore,
    decimal? TotalFinalScore,
    decimal? NormalizedAiScore10,
    decimal? NormalizedFinalScore10,
    bool IsFullyReviewed,
    IReadOnlyList<StudentQuestionResultItem> Questions);

public sealed record SaveTeacherReviewRequest(
    Guid SessionId,
    Guid SessionQuestionId,
    decimal FinalScore,
    string? Comment);

public interface IVivaGradingStore
{
    Task<Guid> FindExamIdBySessionAsync(Guid sessionId, CancellationToken ct);
    Task<ExamGradingOverview> GetExamOverviewAsync(Guid examId, CancellationToken ct);
    Task<VivaSessionGradingSheet> GetSessionGradingSheetAsync(Guid sessionId, CancellationToken ct);
    Task<VivaSessionGradingSheet> GenerateAiGradingsAsync(Guid sessionId, bool forceRegrade, CancellationToken ct);
    Task<VivaSessionGradingSheet> SaveTeacherReviewAsync(SaveTeacherReviewRequest request, CancellationToken ct);
    Task<VivaSessionGradingSheet> ApproveAllAiScoresAsync(Guid sessionId, string? comment, CancellationToken ct);
    Task<StudentSessionGradingResult?> GetStudentResultAsync(Guid sessionId, CancellationToken ct);
}

public interface IVivaGradingService
{
    Task<ExamGradingOverview> GetExamOverviewAsync(Guid examId, CancellationToken ct);
    Task<VivaSessionGradingSheet> GetSessionSheetAsync(Guid sessionId, bool autoGenerateMissingAi, CancellationToken ct);
    Task<VivaSessionGradingSheet> RunAiGradingAsync(Guid sessionId, bool forceRegrade, CancellationToken ct);
    Task<VivaSessionGradingSheet> SaveTeacherReviewAsync(SaveTeacherReviewRequest request, CancellationToken ct);
    Task<VivaSessionGradingSheet> ApproveAllAiScoresAsync(Guid sessionId, string? comment, CancellationToken ct);
    Task<StudentSessionGradingResult?> GetStudentResultAsync(Guid sessionId, CancellationToken ct);
}
