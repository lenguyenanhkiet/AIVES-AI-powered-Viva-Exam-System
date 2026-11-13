using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record StudentQuestion(Guid Id, int Position, string Content);
