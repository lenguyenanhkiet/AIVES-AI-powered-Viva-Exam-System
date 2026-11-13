using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIpoweredVivaExamSystem.Business.Exams;

// SQL owns commit/rollback; Business delivers change events only after a successful commit.
public sealed class NotifyingExamUnitOfWork(IExamUnitOfWork inner, IExamNotifier notifier,
    ILogger<NotifyingExamUnitOfWork>? logger = null) : IExamUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> work, IReadOnlyCollection<Guid> changedExamIds, CancellationToken ct)
    {
        await inner.ExecuteAsync(work, changedExamIds, ct);
        foreach (var examId in changedExamIds.Distinct())
        {
            try { await notifier.NotifyChangedAsync(examId, CancellationToken.None); }
            catch (Exception ex)
            { (logger ?? NullLogger<NotifyingExamUnitOfWork>.Instance).LogWarning(ex, "Exam {ExamId} committed but its notification failed", examId); }
        }
    }
}
