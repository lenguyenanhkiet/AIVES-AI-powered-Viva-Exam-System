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

    public Task<Exam> LockExamAsync(Guid examId, CancellationToken ct) => ExamLock.LockAsync(db, examId, ct);

    public async Task ValidateAndStageAsync(Exam exam, IReadOnlyList<ScheduleSlot> slots, CancellationToken ct)
    {
        RequireTransaction();
        if (await db.VivaSessions.AnyAsync(s => db.ExamStudents.Any(e => e.ExamId == exam.Id && e.Id == s.ExamStudentId), ct))
            throw new ResourceConflictException("Không thể xếp lại lịch cho sinh viên đã có phiên thi.");

        // Guard has authorized this mutation. Conflict detection must include exams hidden by request filters.
        // Only existence is read across owners; no other exam/student data is returned to the caller.
        var otherSlots = ExamSlotQueries.All(db).Where(x => x.Exam.Id != exam.Id &&
            x.Exam.Status != ExamStatus.Cancelled && x.Enrollment.ScheduledAt != null);
        var firstStart = slots[0].StartAt;
        var lastEnd = slots[^1].EndAt;
        if (await otherSlots.AnyAsync(x => x.Exam.LecturerId == exam.LecturerId &&
                x.Enrollment.ScheduledAt < lastEnd && x.EndAt > firstStart, ct))
            throw new ResourceConflictException("Giảng viên có ca thi trùng giờ ở kỳ thi khác.");

        var roster = await db.ExamStudents.Where(x => x.ExamId == exam.Id).ToDictionaryAsync(x => x.Id, ct);
        foreach (var slot in slots)
        {
            var entry = roster[slot.EnrollmentId];
            var studentId = entry.StudentId;
            if (await otherSlots.AnyAsync(x => x.Enrollment.StudentId == studentId &&
                    x.Enrollment.ScheduledAt < slot.EndAt && x.EndAt > slot.StartAt, ct))
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
        await ExamSlotQueries.ForStudent(db, studentId).Where(x => x.Enrollment.ScheduledAt != null)
            .OrderBy(x => x.Enrollment.ScheduledAt).ThenBy(x => x.Enrollment.Id)
            .Select(x => new PersonalSchedule(x.Enrollment.Id, x.Exam.Name, x.Exam.Status,
                x.Enrollment.ScheduledAt!.Value, x.EndAt!.Value)).ToArrayAsync(ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Scheduling mutations require the shared exam transaction.");
    }
}
