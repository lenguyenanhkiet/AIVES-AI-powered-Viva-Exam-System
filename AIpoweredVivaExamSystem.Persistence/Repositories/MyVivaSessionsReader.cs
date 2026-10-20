using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class MyVivaSessionsReader(ApplicationDbContext db, ICurrentUser actor) : IMyVivaSessionsReader
{
    public async Task<IReadOnlyList<MyVivaExam>> ListAsync(CancellationToken ct)
    {
        if (actor.Id == Guid.Empty || actor.IsAdministrator || actor.IsLecturer) throw new UnauthorizedAccessException();
        return await (from r in db.ExamStudents.AsNoTracking()
                      join e in db.Exams.IgnoreQueryFilters() on r.ExamId equals e.Id
                      where r.StudentId == actor.Id && e.DeletedAt == null
                      orderby r.ScheduledAt
                      select new MyVivaExam(r.Id, e.Name, e.Status.ToString(), r.ScheduledAt)).ToArrayAsync(ct);
    }
}
