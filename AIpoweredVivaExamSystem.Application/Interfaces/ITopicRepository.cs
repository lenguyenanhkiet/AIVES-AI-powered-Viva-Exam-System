using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Interfaces;

public interface ITopicRepository
{
    Task<Topic?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> SubjectExistsAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(Guid subjectId, string name, Guid? excludeId, CancellationToken cancellationToken);
    Task<bool> HasActiveQuestionsAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Topic> Items, int TotalCount)> ListAsync(
        Guid? subjectId, string? keyword, AcademicStatus? status, int page, int pageSize,
        CancellationToken cancellationToken);
    void Add(Topic topic);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
