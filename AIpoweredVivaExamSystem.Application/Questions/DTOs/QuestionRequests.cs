using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Questions.DTOs;

public sealed record CreateQuestionRequest(
    Guid SubjectId, Guid? TopicId, string Content, string? ExpectedAnswer,
    BloomLevel BloomLevel, QuestionDifficulty Difficulty);

// SubjectId is fixed after creation. Saving an edit returns the question to Draft.
public sealed record UpdateQuestionRequest(
    Guid? TopicId, string Content, string? ExpectedAnswer,
    BloomLevel BloomLevel, QuestionDifficulty Difficulty);

public sealed class QuestionListQuery
{
    public Guid? SubjectId { get; init; }
    public Guid? TopicId { get; init; }
    public string? Keyword { get; init; }
    public BloomLevel? BloomLevel { get; init; }
    public QuestionDifficulty? Difficulty { get; init; }
    public QuestionStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
