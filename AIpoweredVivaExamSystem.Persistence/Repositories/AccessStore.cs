using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class AccessStore(ApplicationDbContext db) : IAccessStore
{
    public async Task<AccessOverview> OverviewAsync(CancellationToken ct) => new(
        await db.Users.Where(u => u.DeletedAt == null).OrderBy(u => u.Email).Select(u => new AccessUser(u.Id, u.Email, u.FullName, u.Role)).ToArrayAsync(ct),
        await db.Subjects.Where(s => s.DeletedAt == null).OrderBy(s => s.Code).Select(s => new AccessSubject(s.Id, s.Code)).ToArrayAsync(ct),
        await (from a in db.LecturerSubjects join u in db.Users on a.LecturerId equals u.Id
               join s in db.Subjects on a.SubjectId equals s.Id select new SubjectAssignment(a.Id, u.Email, s.Code)).ToArrayAsync(ct));
    public Task<User?> FindUserAsync(Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
    public Task<bool> SubjectExistsAsync(Guid id, CancellationToken ct) => db.Subjects.AnyAsync(x => x.Id == id && x.DeletedAt == null, ct);
    public Task<bool> AssignmentExistsAsync(Guid lecturerId, Guid subjectId, CancellationToken ct) => db.LecturerSubjects.AnyAsync(x => x.LecturerId == lecturerId && x.SubjectId == subjectId, ct);
    public void AddAssignment(LecturerSubject assignment) => db.LecturerSubjects.Add(assignment);
    public async Task RemoveAssignmentAsync(Guid id, CancellationToken ct)
    { var assignment = await db.LecturerSubjects.SingleOrDefaultAsync(x => x.Id == id, ct); if (assignment is not null) db.Remove(assignment); }
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ResourceConflictException("This assignment already exists."); }
    }
}
