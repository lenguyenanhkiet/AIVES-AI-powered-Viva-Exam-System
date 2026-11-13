using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record TurnEvidence(Guid TurnId, AnswerType AnswerType, int FollowUpLevel, string PromptText, string? Transcript,
    DateTimeOffset AskedAt, DateTimeOffset? AnsweredAt, bool TimedOut, double AnswerSeconds, int TurnLimitSeconds,
    int? AudioDurationSeconds, int AnswerLength, string? AudioUrl, string? AudioMimeType);
