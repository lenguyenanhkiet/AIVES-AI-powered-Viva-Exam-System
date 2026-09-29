using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Questions.DTOs;

public sealed record QuestionResponse(Guid Id, Guid SubjectId, Guid? TopicId, string Content,
    string? ExpectedAnswer, BloomLevel BloomLevel, QuestionDifficulty Difficulty,
    QuestionSourceType SourceType, QuestionStatus Status, bool HasRubric,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
