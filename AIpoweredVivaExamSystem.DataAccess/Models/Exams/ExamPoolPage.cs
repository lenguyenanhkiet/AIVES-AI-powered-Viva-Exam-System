using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamPoolPage(ExamSummary Exam, int RequiredQuestions, int CurrentPoolCount,
    int EligiblePoolCount, IReadOnlyList<ExamPoolQuestion> AvailableQuestions,
    IReadOnlyList<ExamPoolQuestion> PooledQuestions);
