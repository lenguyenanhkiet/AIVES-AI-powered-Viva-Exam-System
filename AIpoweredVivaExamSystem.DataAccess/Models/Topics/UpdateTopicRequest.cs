using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Topics;

public sealed record UpdateTopicRequest(string Name, string? Description, AcademicStatus Status);
