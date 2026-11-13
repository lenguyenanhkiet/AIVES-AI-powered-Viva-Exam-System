using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Scheduling;

public sealed record ScheduleEnrollment(Guid EnrollmentId, Guid StudentId, string StudentName, string Email,
    DateTimeOffset? ScheduledAt, byte[] RowVersion);
