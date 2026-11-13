using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;

public interface IExamUnitOfWork
{
    // Work may stage entities only. No external calls or independent commits inside the delegate.
    // changedExamIds are delivered only after a successful database commit.
    Task ExecuteAsync(Func<CancellationToken, Task> work, IReadOnlyCollection<Guid> changedExamIds, CancellationToken ct);
}
