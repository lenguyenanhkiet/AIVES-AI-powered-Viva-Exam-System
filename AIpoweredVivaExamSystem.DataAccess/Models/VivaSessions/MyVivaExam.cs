using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record MyVivaExam(Guid EnrollmentId, string Name, ExamStatus Status,
    DateTimeOffset? ScheduledAt, DateTimeOffset? EndAt);
