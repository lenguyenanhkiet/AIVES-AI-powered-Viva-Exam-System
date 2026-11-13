using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record StudentSession(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, IReadOnlyList<StudentQuestion> Questions);
