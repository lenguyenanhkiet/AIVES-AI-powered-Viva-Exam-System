using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Interfaces;

public interface IQuestionRepository
{
    Task<Question?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> SubjectExistsAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<Topic?> GetTopicAsync(Guid topicId, CancellationToken cancellationToken);
    Task<bool> HasRubricAsync(Guid id, CancellationToken cancellationToken);
    Task RemoveRubricAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<(Question Question, bool HasRubric)> Items, int TotalCount)> ListAsync(
        Guid? subjectId, Guid? topicId, string? keyword, BloomLevel? bloomLevel,
        QuestionDifficulty? difficulty, QuestionStatus? status, int page, int pageSize,
        CancellationToken cancellationToken);
    void Add(Question question);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
