using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamLiveReader(ApplicationDbContext db, IExamFoundationReader foundation) : IExamLiveReader
{
    public Task<ExamSummary> SummaryAsync(Guid examId, CancellationToken ct) => db.Exams.AsNoTracking()
        .Where(e => e.Id == examId && e.DeletedAt == null)
        .Select(e => new ExamSummary(e.Id, e.SubjectId, e.Name, e.Status, e.StartAt, e.EndAt)).SingleAsync(ct);
    public async Task<ExamLiveStatus> ReadAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == examId && e.DeletedAt == null, ct);
        var roster = await (from r in db.ExamStudents.AsNoTracking()
                            join u in db.Users on r.StudentId equals u.Id
                            where r.ExamId == examId
                            select new LiveStudent(r.Id, u.FullName, r.ScheduledAt,
                                db.VivaSessions.Any(s => s.ExamStudentId == r.Id && s.CompletedAt != null) ? "Completed" :
                                db.VivaSessions.Any(s => s.ExamStudentId == r.Id) ? "In progress" : "Not started"))
                            .ToArrayAsync(ct);
        return new(exam.Status.ToString(), await foundation.CountsAsync(examId, ct),
            roster.Count(r => r.SessionStatus == "Completed"), roster);
    }
}
