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
        return await ExamSlotQueries.ForStudent(db, actor.Id)
            .OrderBy(x => x.Enrollment.ScheduledAt).ThenBy(x => x.Enrollment.Id)
            .Select(x => new MyVivaExam(x.Enrollment.Id, x.Exam.Name, x.Exam.Status,
                x.Enrollment.ScheduledAt, x.EndAt)).ToArrayAsync(ct);
    }
}
