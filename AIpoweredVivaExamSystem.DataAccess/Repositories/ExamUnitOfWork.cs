using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

public sealed class ExamUnitOfWork(ApplicationDbContext db) : IExamUnitOfWork
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
    }
}
