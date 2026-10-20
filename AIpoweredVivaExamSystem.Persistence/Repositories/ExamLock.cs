using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

internal static class ExamLock
{
    // Call only after authorization, and always before locking an enrollment.
    public static async Task<Exam> LockAsync(ApplicationDbContext db, Guid examId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Exam mutations require the shared exam transaction.");

        var exam = await db.Exams.FromSqlInterpolated(
                $"SELECT * FROM [Exams] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {examId}")
            .IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(x => x.DeletedAt == null, ct)
            ?? throw new ResourceNotFoundException("Exam not found.");
        // A reused scoped context may already track an older lifecycle/version.
        await db.Entry(exam).ReloadAsync(ct);
        return exam;
    }
}
