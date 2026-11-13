using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Scheduling;

public sealed record PersonalSchedule(Guid EnrollmentId, string ExamName, ExamStatus Status,
    DateTimeOffset StartAt, DateTimeOffset EndAt);
