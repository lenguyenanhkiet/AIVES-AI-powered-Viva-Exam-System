using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamConfiguration(Guid SubjectId, string Name, DateTimeOffset StartAt, DateTimeOffset EndAt,
    int DurationMinutes, int NumberOfMainQuestions, int MaxFollowUpPerQuestion);
