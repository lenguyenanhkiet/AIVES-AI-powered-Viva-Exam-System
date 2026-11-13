using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Questions;

public sealed record UpdateQuestionRequest(
    Guid? TopicId, string Content, string? ExpectedAnswer,
    BloomLevel BloomLevel, QuestionDifficulty Difficulty);
