using AIpoweredVivaExamSystem.Application.Exams;
using ContractExamStatus = AIpoweredVivaExamSystem.Application.Contracts.ExamStatus;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamLiveReader(ApplicationDbContext db, IExamFoundationReader foundation) : IExamLiveReader
{
    public async Task<ExamSummary> SummaryAsync(Guid examId, CancellationToken ct)
    {
        var row = await db.Exams.AsNoTracking()
        .Where(e => e.Id == examId && e.DeletedAt == null)
        .Select(e => new { e.Id, e.SubjectId, e.Name, e.Status, e.StartAt, e.EndAt })
        .SingleAsync(ct);
        return new(row.Id, row.SubjectId, row.Name, (ContractExamStatus)row.Status,
            row.StartAt, row.EndAt);
    }
    public async Task<ExamLiveStatus> ReadAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == examId && e.DeletedAt == null, ct);
        var roster = await (from r in db.ExamStudents.AsNoTracking()
                            join u in db.Users on r.StudentId equals u.Id
                            where r.ExamId == examId
                            select new LiveStudent(r.Id, u.FullName, r.ScheduledAt,
                                db.VivaSessions.Any(s => s.ExamStudentId == r.Id && s.CompletedAt != null) ? SessionState.Completed :
                                db.VivaSessions.Any(s => s.ExamStudentId == r.Id) ? SessionState.InProgress : SessionState.NotStarted))
                            .ToArrayAsync(ct);
        return new((ContractExamStatus)exam.Status, await foundation.CountsAsync(examId, ct),
            roster.Count(r => r.SessionStatus == SessionState.Completed), roster);
    }
}
