using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

internal sealed class ExamEnrollmentSlot
{
    public required ExamStudent Enrollment { get; init; }
    public required Exam Exam { get; init; }
    public DateTimeOffset? EndAt { get; init; }
}

internal static class ExamSlotQueries
{
    // Cross-owner conflict checks require authorization; student reads must use ForStudent.
    public static IQueryable<ExamEnrollmentSlot> All(ApplicationDbContext db) =>
        from entry in db.ExamStudents.IgnoreQueryFilters().AsNoTracking()
        join exam in db.Exams.IgnoreQueryFilters().AsNoTracking() on entry.ExamId equals exam.Id
        where exam.DeletedAt == null
        select new ExamEnrollmentSlot
        {
            Enrollment = entry, Exam = exam,
            // SQL translation of Exam.SlotEnd: EF cannot translate the domain method.
            EndAt = entry.ScheduledAt.HasValue
                ? entry.ScheduledAt.Value.AddMinutes(exam.DurationMinutes) : null
        };

    public static IQueryable<ExamEnrollmentSlot> ForStudent(ApplicationDbContext db, Guid studentId) =>
        All(db).Where(x => x.Enrollment.StudentId == studentId &&
            (x.Exam.Status == ExamStatus.Published || x.Exam.Status == ExamStatus.Ongoing ||
             x.Exam.Status == ExamStatus.Completed));
}
