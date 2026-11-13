using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

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
    bool HasAudio,
    bool TimedOut = false);
