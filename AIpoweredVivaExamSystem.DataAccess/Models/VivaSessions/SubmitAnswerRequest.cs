using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record SubmitAnswerRequest(
    Guid SessionId,
    Guid TurnId,
    string? Transcript,
    byte[]? AudioBytes = null,
    string? AudioMimeType = null,
    int? AudioDurationSeconds = null,
    string Language = "vi",
    bool TimedOut = false);
