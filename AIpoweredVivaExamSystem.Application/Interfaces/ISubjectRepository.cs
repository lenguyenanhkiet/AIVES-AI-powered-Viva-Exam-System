using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Interfaces;

public interface ISubjectRepository
{
    Task<Subject?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);
    Task<bool> HasActiveDependentsAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Subject> Items, int TotalCount)> ListAsync(
        string? keyword, AcademicStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    void Add(Subject subject);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
