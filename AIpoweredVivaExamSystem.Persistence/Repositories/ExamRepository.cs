using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamRepository(ApplicationDbContext db) : IExamRepository
{
    public Task<Exam?> FindByIdAsync(Guid examId, CancellationToken ct) =>
        db.Exams.SingleOrDefaultAsync(e => e.Id == examId && e.DeletedAt == null, ct);

    public void Add(Exam exam) => db.Exams.Add(exam);

    public void Remove(Exam exam)
    {
        exam.DeletedAt = DateTimeOffset.UtcNow;
    }
}
