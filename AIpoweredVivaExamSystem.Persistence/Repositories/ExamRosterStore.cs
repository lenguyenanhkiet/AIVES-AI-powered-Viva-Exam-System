using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.ExamStudents;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamRosterStore(ApplicationDbContext db) : IExamRosterStore
{
    public async Task<RosterExamPage> ListManagedAsync(int page, int pageSize, CancellationToken ct)
    {
        // Keep request-aware filters: pagination must never reveal another lecturer's exams.
        var query = db.Exams.AsNoTracking().Where(x => x.DeletedAt == null);
        var total = await query.CountAsync(ct);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);
        var items = await query.OrderByDescending(x => x.StartAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ExamSummary(x.Id, x.SubjectId, x.Name, x.Status, x.StartAt, x.EndAt)).ToArrayAsync(ct);
        return new(items, page, pageSize, total);
    }

    public Task<ExamSummary?> FindExamSummaryAsync(Guid examId, CancellationToken ct) =>
        db.Exams.AsNoTracking().Where(x => x.Id == examId && x.DeletedAt == null)
            .Select(x => new ExamSummary(x.Id, x.SubjectId, x.Name, x.Status, x.StartAt, x.EndAt))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RosterEntry>> ListAsync(Guid examId, CancellationToken ct) =>
        await (from enrollment in db.ExamStudents.AsNoTracking()
               join student in db.Users.AsNoTracking() on enrollment.StudentId equals student.Id
               where enrollment.ExamId == examId
               orderby enrollment.StudentId
               select new RosterEntry(enrollment.Id, student.Id, student.FullName,
                   enrollment.ScheduledAt, enrollment.RowVersion, student.Email)).ToArrayAsync(ct);

    public Task<Exam> LockExamAsync(Guid examId, CancellationToken ct) => ExamLock.LockAsync(db, examId, ct);

    public Task<User?> FindStudentAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, ct);

    public Task<bool> ContainsAsync(Guid examId, Guid studentId, CancellationToken ct) =>
        db.ExamStudents.AnyAsync(x => x.ExamId == examId && x.StudentId == studentId, ct);

    public Task<ExamStudent?> FindEnrollmentAsync(Guid examId, Guid enrollmentId, CancellationToken ct) =>
        db.ExamStudents.SingleOrDefaultAsync(x => x.ExamId == examId && x.Id == enrollmentId, ct);

    public Task<bool> HasSessionAsync(Guid enrollmentId, CancellationToken ct) =>
        db.VivaSessions.AnyAsync(x => x.ExamStudentId == enrollmentId, ct);

    // Save/commit/notify belong to ExamUnitOfWork, never to repository methods.
    public void Add(ExamStudent enrollment) => db.ExamStudents.Add(enrollment);
    public void Remove(ExamStudent enrollment) => db.ExamStudents.Remove(enrollment);
}
