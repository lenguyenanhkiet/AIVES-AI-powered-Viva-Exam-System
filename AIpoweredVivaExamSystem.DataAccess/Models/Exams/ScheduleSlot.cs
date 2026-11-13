using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ScheduleSlot(Guid EnrollmentId, DateTimeOffset StartAt, DateTimeOffset EndAt);
