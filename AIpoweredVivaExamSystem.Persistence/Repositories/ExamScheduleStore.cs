using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Scheduling;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamScheduleStore(ApplicationDbContext db) : IExamScheduleStore
{
    public async Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct) =>
        await db.Exams.AsNoTracking().Where(x => x.DeletedAt == null)
            .OrderByDescending(x => x.StartAt).ThenBy(x => x.Id)
            .Select(x => new ExamSummary(x.Id, x.SubjectId, x.Name, x.Status, x.StartAt, x.EndAt)).ToArrayAsync(ct);

    public Task<Exam?> FindExamAsync(Guid examId, CancellationToken ct) =>
        db.Exams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == examId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ScheduleEnrollment>> ListRosterAsync(Guid examId, CancellationToken ct)
    {
        // Sort Guids in Application too, so SQL's uniqueidentifier ordering cannot change the preview order.
        var rows = await (from entry in db.ExamStudents.AsNoTracking()
                          join student in db.Users.AsNoTracking() on entry.StudentId equals student.Id
                          where entry.ExamId == examId
                          select new ScheduleEnrollment(entry.Id, entry.StudentId, student.FullName,
                              student.Email, entry.ScheduledAt, entry.RowVersion)).ToArrayAsync(ct);
        return rows.OrderBy(x => x.StudentId).ToArray();
    }

    public async Task LockSchedulingAsync(CancellationToken ct)
    {
        RequireTransaction();
        // A transaction-owned SQL mutex closes the cross-exam overlap race. Acquire before any exam lock.
        // Scheduling applies are serialized database-wide; reads and session operations remain independent.
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = N'AIVES:ExamScheduling',
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 51000, 'Scheduling is busy. Try again.', 1;
                """, ct);
        }
        catch (SqlException ex) when (ex.Number == 51000)
        { throw new ResourceConflictException("Hệ thống đang xếp lịch. Vui lòng thử lại."); }
    }

    public async Task<Exam> LockExamAsync(Guid examId, CancellationToken ct)
    {
        RequireTransaction();
        var exam = await db.Exams.FromSqlInterpolated(
                $"SELECT * FROM [Exams] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {examId}")
            .SingleOrDefaultAsync(x => x.DeletedAt == null, ct)
            ?? throw new ResourceNotFoundException("Exam not found.");
        await db.Entry(exam).ReloadAsync(ct);
        return exam;
    }

    public async Task ValidateAndStageAsync(Exam exam, IReadOnlyList<ScheduleSlot> slots, CancellationToken ct)
    {
        RequireTransaction();
        if (await db.VivaSessions.AnyAsync(s => db.ExamStudents.Any(e => e.ExamId == exam.Id && e.Id == s.ExamStudentId), ct))
            throw new ResourceConflictException("Không thể xếp lại lịch cho sinh viên đã có phiên thi.");

        // Guard has authorized this mutation. Conflict detection must include exams hidden by request filters.
        // Only existence is read across owners; no other exam/student data is returned to the caller.
        var otherSlots = from entry in db.ExamStudents.IgnoreQueryFilters().AsNoTracking()
                         join other in db.Exams.IgnoreQueryFilters().AsNoTracking() on entry.ExamId equals other.Id
                         where other.Id != exam.Id && other.DeletedAt == null &&
                               other.Status != ExamStatus.Cancelled && entry.ScheduledAt != null
                         select new { entry.StudentId, other.LecturerId, Start = entry.ScheduledAt!.Value, other.DurationMinutes };
        var firstStart = slots[0].StartAt;
        var lastEnd = slots[^1].EndAt;
        if (await otherSlots.AnyAsync(x => x.LecturerId == exam.LecturerId &&
                x.Start < lastEnd && x.Start.AddMinutes(x.DurationMinutes) > firstStart, ct))
            throw new ResourceConflictException("Giảng viên có ca thi trùng giờ ở kỳ thi khác.");

        var roster = await db.ExamStudents.Where(x => x.ExamId == exam.Id).ToDictionaryAsync(x => x.Id, ct);
        foreach (var slot in slots)
        {
            var entry = roster[slot.EnrollmentId];
            var studentId = entry.StudentId;
            if (await otherSlots.AnyAsync(x => x.StudentId == studentId &&
                    x.Start < slot.EndAt && x.Start.AddMinutes(x.DurationMinutes) > slot.StartAt, ct))
                throw new ResourceConflictException("Sinh viên có ca thi trùng giờ ở kỳ thi khác.");
        }
        // Validate the whole batch before staging any change. Reload handles a reused scoped DbContext.
        foreach (var slot in slots)
        {
            var entry = roster[slot.EnrollmentId];
            await db.Entry(entry).ReloadAsync(ct);
            entry.Schedule(slot.StartAt);
        }
    }

    public async Task<IReadOnlyList<PersonalSchedule>> MyScheduleAsync(Guid studentId, CancellationToken ct) =>
        // The service requires the authenticated actor. Explicit own-enrollment scope is mandatory here:
        // students intentionally cannot read Exams through its manager-only query filter.
        await (from entry in db.ExamStudents.IgnoreQueryFilters().AsNoTracking()
               join exam in db.Exams.IgnoreQueryFilters().AsNoTracking() on entry.ExamId equals exam.Id
               where entry.StudentId == studentId && entry.ScheduledAt != null &&
                     exam.DeletedAt == null && (exam.Status == ExamStatus.Published ||
                         exam.Status == ExamStatus.Ongoing || exam.Status == ExamStatus.Completed)
               orderby entry.ScheduledAt, entry.Id
               select new PersonalSchedule(entry.Id, exam.Name, exam.Status, entry.ScheduledAt!.Value,
                   entry.ScheduledAt!.Value.AddMinutes(exam.DurationMinutes))).ToArrayAsync(ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Scheduling mutations require the shared exam transaction.");
    }
}
