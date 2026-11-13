using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record ExaminerDecision(
    bool IsComplete,
    string? MissingConcepts,
    string? AnalysisSummary,
    string? FollowUpPrompt,
    string ModelName);
