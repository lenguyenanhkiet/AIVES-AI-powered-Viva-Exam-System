namespace AIpoweredVivaExamSystem.Business.Exams;

public sealed class ExamRealtimeAccess(ILoginUserRepository users, IExamAccessStore exams)
{
    public Task<bool> CanManageAsync(Guid userId, Guid examId, CancellationToken ct) =>
        CanManageCoreAsync(userId, examId, false, ct);

    public Task<bool> CanManageListAsync(Guid userId, Guid examId, CancellationToken ct) =>
        CanManageCoreAsync(userId, examId, true, ct);

    private async Task<bool> CanManageCoreAsync(Guid userId, Guid examId, bool includeDeleted, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null || user.DeletedAt != null || user.Status != UserStatus.Active) return false;
        var exam = includeDeleted
            ? await exams.FindExamForNotificationAsync(examId, ct)
            : await exams.FindExamAsync(examId, ct);
        return exam is not null && (user.Role == UserRole.ADMIN ||
            (user.Role == UserRole.LECTURER && exam.LecturerId == userId && await exams.IsAssignedAsync(userId, exam.SubjectId, ct)));
    }
}
