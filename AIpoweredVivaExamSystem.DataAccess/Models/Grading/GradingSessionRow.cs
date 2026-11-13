using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record GradingSessionRow(Guid EnrollmentId, Guid? SessionId, string StudentName, string StudentEmail,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string State, decimal? FinalTotal, decimal? SuggestedTotal, decimal? LecturerTotal, decimal MaxTotal);
