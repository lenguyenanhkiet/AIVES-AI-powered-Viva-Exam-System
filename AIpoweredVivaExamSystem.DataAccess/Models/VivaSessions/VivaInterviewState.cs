using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

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
    IReadOnlyList<StudentQuestion> Questions,
    AIpoweredVivaExamSystem.DataAccess.Models.Grading.StudentFinalResult? Result = null);
