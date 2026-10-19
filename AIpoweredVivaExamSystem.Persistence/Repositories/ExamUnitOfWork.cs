using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamUnitOfWork(ApplicationDbContext db, IExamNotifier notifier, ILogger<ExamUnitOfWork> logger) : IExamUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> work, IReadOnlyCollection<Guid> changedExamIds, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
            throw new InvalidOperationException("ExamUnitOfWork owns its transaction; nested transactions are not supported.");
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                await work(ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            { db.ChangeTracker.Clear(); throw new ResourceConflictException("Exam data changed. Reload and try again."); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 or 547 })
            { db.ChangeTracker.Clear(); throw new ResourceConflictException("The operation conflicts with existing or related exam data."); }
            catch { db.ChangeTracker.Clear(); throw; }
        }
        // The transaction has committed and disposed. Notification failure cannot undo persisted work.
        foreach (var examId in changedExamIds.Distinct())
        {
            try { await notifier.NotifyChangedAsync(examId, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "Exam {ExamId} committed but its notification failed", examId); }
        }
    }
}
