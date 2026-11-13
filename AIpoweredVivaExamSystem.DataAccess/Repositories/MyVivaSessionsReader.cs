using AIpoweredVivaExamSystem.DataAccess.Contracts;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

public sealed class MyVivaSessionsReader(ApplicationDbContext db, IDataActor actor) : IMyVivaSessionsReader
{
    public async Task<IReadOnlyList<MyVivaExam>> ListAsync(CancellationToken ct)
    {
        if (actor.Id == Guid.Empty || actor.IsAdministrator || actor.IsLecturer) throw new UnauthorizedAccessException();
        var rows = await ExamSlotQueries.ForStudent(db, actor.Id)
            .OrderBy(x => x.Enrollment.ScheduledAt).ThenBy(x => x.Enrollment.Id)
            .Select(x => new { x.Enrollment.Id, x.Exam, x.Enrollment.ScheduledAt, x.EndAt,
                HasSession = db.VivaSessions.Any(s => s.ExamStudentId == x.Enrollment.Id) })
            .ToArrayAsync(ct);

        return rows.Select(x =>
        {
            var entry = x.Exam.EntryWindow(x.ScheduledAt);
            return new MyVivaExam(x.Id, x.Exam.Name, x.Exam.Status, x.ScheduledAt, x.EndAt,
                x.HasSession, entry?.OpensAt, entry?.ClosesAt);
        }).ToArray();
    }
}
