using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

public sealed class ExamRepository(ApplicationDbContext db) : IExamRepository
{
    public Task<Exam?> FindByIdAsync(Guid examId, CancellationToken ct) =>
        db.Exams.SingleOrDefaultAsync(e => e.Id == examId && e.DeletedAt == null, ct);

    public Task<Exam> LockAsync(Guid examId, CancellationToken ct) => ExamLock.LockAsync(db, examId, ct);

    public void Add(Exam exam) => db.Exams.Add(exam);

    public void Remove(Exam exam)
    {
        exam.DeletedAt = DateTimeOffset.UtcNow;
    }
}
