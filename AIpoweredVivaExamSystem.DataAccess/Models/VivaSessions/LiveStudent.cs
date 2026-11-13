using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;
namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record LiveStudent(Guid EnrollmentId, string Name, DateTimeOffset? ScheduledAt, SessionState SessionStatus);
