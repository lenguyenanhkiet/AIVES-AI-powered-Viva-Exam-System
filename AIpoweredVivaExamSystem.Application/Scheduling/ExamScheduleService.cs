using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Scheduling;

public sealed class ExamScheduleService(IExamScheduleStore store, ExamAccessGuard guard,
    IExamUnitOfWork unitOfWork, ICurrentUser actor, IExamFoundationReader foundation) : IExamScheduleService, IScheduleReader, IScheduleReadiness
{
    public Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct)
    {
        guard.RequireManager();
        return foundation.ListManagedAsync(ct);
    }

    public async Task<ExamSchedule> GetAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var exam = await FindAsync(examId, ct);
        return new(new(exam.Id, exam.SubjectId, exam.Name, exam.Status, exam.StartAt, exam.EndAt),
            exam.DurationMinutes, await store.ListRosterAsync(examId, ct));
    }

    public async Task<SchedulePreview> PreviewAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var exam = await FindAsync(examId, ct);
        RequireDraft(exam);
        return SequentialSchedule.Preview(exam, await store.ListRosterAsync(examId, ct));
    }

    public async Task ApplyAsync(Guid examId, string previewToken, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        await unitOfWork.ExecuteAsync(async token =>
        {
            // Serialize scheduling across exams as well: exam locks alone cannot protect shared students.
            await store.LockSchedulingAsync(token);
            var exam = await store.LockExamAsync(examId, token);
            await guard.CanManageExamAsync(examId, token);
            RequireDraft(exam);
            SchedulePreview preview;
            try { preview = SequentialSchedule.Preview(exam, await store.ListRosterAsync(examId, token)); }
            catch (DomainValidationException)
            { throw new ResourceConflictException("Danh sách hoặc cửa sổ thi đã thay đổi. Hãy xem trước lịch mới."); }
            if (!string.Equals(preview.PreviewToken, previewToken, StringComparison.Ordinal))
                throw new ResourceConflictException("Bản xem trước đã cũ. Hãy xem trước lịch mới rồi lưu lại.");
            await store.ValidateAndStageAsync(exam, preview.Slots, token);
            exam.UpdatedAt = DateTimeOffset.UtcNow;
        }, [examId], ct);
    }

    public async Task<IReadOnlyList<ScheduleSlot>> MyScheduleAsync(CancellationToken ct) =>
        (await MyScheduleDetailsAsync(ct)).Select(x => new ScheduleSlot(x.EnrollmentId, x.StartAt, x.EndAt)).ToArray();

    public Task<IReadOnlyList<PersonalSchedule>> MyScheduleDetailsAsync(CancellationToken ct)
    {
        if (actor.Id == Guid.Empty) throw new UnauthorizedAccessException("Authentication is required.");
        return store.MyScheduleAsync(actor.Id, ct);
    }

    public async Task<bool> IsFullyScheduledAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var exam = await FindAsync(examId, ct);
        var roster = await store.ListRosterAsync(examId, ct);
        return roster.Count > 0 && roster.All(x => x.ScheduledAt is { } start &&
            start >= exam.StartAt && exam.SlotEnd(start) <= exam.EndAt);
    }

    private async Task<Exam> FindAsync(Guid examId, CancellationToken ct) =>
        await store.FindExamAsync(examId, ct) ?? throw new ResourceNotFoundException("Exam not found.");

    private static void RequireDraft(Exam exam)
    {
        if (exam.Status != ExamStatus.Draft)
            throw new ResourceConflictException("Chỉ xếp lịch khi kỳ thi ở trạng thái Draft.");
    }
}
