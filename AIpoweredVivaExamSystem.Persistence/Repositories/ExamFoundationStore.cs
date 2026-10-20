using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamFoundationStore(ApplicationDbContext db) : IExamAccessStore, IExamFoundationReader
{
    // Ignore filters here only to obtain the minimal access facts; the Application guard makes the decision.
    public Task<ExamAccess?> FindExamAsync(Guid examId, CancellationToken ct) => db.Exams.IgnoreQueryFilters()
        .Where(e => e.Id == examId && e.DeletedAt == null).Select(e => new ExamAccess(e.Id, e.SubjectId, e.LecturerId)).SingleOrDefaultAsync(ct);
    public Task<ExamStudentAccess?> FindEnrollmentAsync(Guid enrollmentId, CancellationToken ct) => db.ExamStudents.IgnoreQueryFilters()
        .Where(e => e.Id == enrollmentId && db.Exams.IgnoreQueryFilters().Any(x => x.Id == e.ExamId && x.DeletedAt == null))
        .Select(e => new ExamStudentAccess(e.Id, e.ExamId, e.StudentId)).SingleOrDefaultAsync(ct);
    public Task<bool> IsAssignedAsync(Guid lecturerId, Guid subjectId, CancellationToken ct) => db.LecturerSubjects.AnyAsync(a => a.LecturerId == lecturerId && a.SubjectId == subjectId &&
        db.Subjects.IgnoreQueryFilters().Any(s => s.Id == subjectId && s.DeletedAt == null), ct);
    public Task<bool> IsEnrolledAsync(Guid examId, Guid studentId, CancellationToken ct) => db.ExamStudents.IgnoreQueryFilters().AnyAsync(e => e.ExamId == examId && e.StudentId == studentId &&
        db.Exams.IgnoreQueryFilters().Any(x => x.Id == examId && x.DeletedAt == null), ct);
    public async Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct) => await db.Exams.AsNoTracking().Where(e => e.DeletedAt == null)
        .OrderByDescending(e => e.StartAt).Select(e => new ExamSummary(e.Id, e.SubjectId, e.Name, e.Status, e.StartAt, e.EndAt)).Take(100).ToArrayAsync(ct);
    public async Task<ExamFoundationCounts> CountsAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.SingleAsync(e => e.Id == examId, ct);
        return new(
            await ExamPoolQueries.EligiblePooledQuestions(db, exam).CountAsync(ct),
            await db.ExamStudents.CountAsync(s => s.ExamId == examId, ct),
            await db.ExamStudents.CountAsync(s => s.ExamId == examId && s.ScheduledAt != null, ct),
            await db.VivaSessions.CountAsync(s => db.ExamStudents.Any(r => r.Id == s.ExamStudentId && r.ExamId == examId), ct));
    }
}
