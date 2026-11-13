using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamSummary(Guid Id, Guid SubjectId, string Name, ExamStatus Status,
    DateTimeOffset StartAt, DateTimeOffset EndAt);
