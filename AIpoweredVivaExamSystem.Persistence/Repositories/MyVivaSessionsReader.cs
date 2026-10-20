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
        return await ExamSlotQueries.ForStudent(db, actor.Id)
            .OrderBy(x => x.Enrollment.ScheduledAt).ThenBy(x => x.Enrollment.Id)
            .Select(x => new MyVivaExam(x.Enrollment.Id, x.Exam.Name, x.Exam.Status,
                x.Enrollment.ScheduledAt, x.EndAt)).ToArrayAsync(ct);
    }
}
