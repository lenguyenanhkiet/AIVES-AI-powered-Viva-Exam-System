using AIpoweredVivaExamSystem.DataAccess.Contracts.Grading;
using AIpoweredVivaExamSystem.DataAccess.Models.Grading;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

public sealed class StudentResultAccess(ApplicationDbContext db) : IStudentResultAccess
{
    public Task<bool> CanReadAsync(Guid studentId, Guid sessionId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Id == studentId && user.DeletedAt == null &&
            user.Status == UserStatus.Active && user.Role == UserRole.STUDENT &&
            db.VivaSessions.IgnoreQueryFilters().Any(session => session.Id == sessionId && session.CompletedAt != null &&
                db.ExamStudents.IgnoreQueryFilters().Any(entry => entry.Id == session.ExamStudentId && entry.StudentId == studentId)), ct);
}
