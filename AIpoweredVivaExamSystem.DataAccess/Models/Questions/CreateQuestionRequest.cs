using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Questions;

public sealed record CreateQuestionRequest(
    Guid SubjectId, Guid? TopicId, string Content, string? ExpectedAnswer,
    BloomLevel BloomLevel, QuestionDifficulty Difficulty);

// SubjectId is fixed after creation. Saving an edit returns the question to Draft.
