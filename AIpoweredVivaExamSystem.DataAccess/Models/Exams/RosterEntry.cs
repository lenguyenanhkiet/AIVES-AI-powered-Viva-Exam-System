using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record RosterEntry(Guid EnrollmentId, Guid StudentId, string Name, DateTimeOffset? ScheduledAt, byte[] RowVersion, string Email = "");
