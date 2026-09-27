using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Interfaces;

public interface IRubricRepository
{
    Task<Rubric?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsForQuestionAsync(Guid questionId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Rubric> Items, int TotalCount)> ListAsync(
        Guid? questionId, int page, int pageSize, CancellationToken cancellationToken);
    void Add(Rubric rubric);
    void MarkUpdated(Rubric rubric);
    void Remove(Rubric rubric);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
