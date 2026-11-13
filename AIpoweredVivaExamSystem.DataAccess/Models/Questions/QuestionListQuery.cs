using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Questions;

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
