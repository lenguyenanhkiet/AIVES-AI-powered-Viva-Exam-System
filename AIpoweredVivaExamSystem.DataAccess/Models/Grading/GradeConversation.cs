using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record GradeConversation(string Prompt, string? Transcript);
// Deliberately contains no time, audio-duration, length or other secondary signals.
