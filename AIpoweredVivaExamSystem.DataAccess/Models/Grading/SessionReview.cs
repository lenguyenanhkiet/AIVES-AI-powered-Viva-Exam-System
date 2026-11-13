using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record SessionReview(Guid SessionId, Guid ExamId, string ExamName, string StudentName, string StudentEmail,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string State, string Version, bool CanEdit, bool CanReopen,
    Guid? PreviousSessionId, Guid? NextSessionId, IReadOnlyList<QuestionReview> Questions, string? OverallComment,
    decimal? SuggestedTotal, decimal? LecturerTotal, decimal MaxTotal, DateTimeOffset? FinalisedAt,
    string? FinalisedByName, EvidenceSignals Signals);
