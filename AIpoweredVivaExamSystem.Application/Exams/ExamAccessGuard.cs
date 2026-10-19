using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;

namespace AIpoweredVivaExamSystem.Application.Exams;

public sealed class ExamAccessGuard(IExamAccessStore store, ICurrentUser actor)
{
    public void RequireManager()
    {
        if (actor.Id == Guid.Empty || (!actor.IsAdministrator && !actor.IsLecturer))
            throw new UnauthorizedAccessException("Lecturer or administrator access is required.");
    }
    public async Task RequireSubjectAsync(Guid subjectId, CancellationToken ct)
    {
        RequireManager();
        if (!actor.IsAdministrator && !await store.IsAssignedAsync(actor.Id, subjectId, ct))
            throw new UnauthorizedAccessException("The lecturer is not assigned to this subject.");
    }
    public async Task<ExamAccess> CanManageExamAsync(Guid examId, CancellationToken ct)
    {
        RequireManager();
        var exam = await store.FindExamAsync(examId, ct) ?? throw new ResourceNotFoundException("Exam not found.");
        if (!actor.IsAdministrator && (exam.LecturerId != actor.Id || !await store.IsAssignedAsync(actor.Id, exam.SubjectId, ct)))
            throw new UnauthorizedAccessException("You cannot manage this exam.");
        return exam;
    }
    public async Task<ExamStudentAccess> OwnExamStudentAsync(Guid enrollmentId, CancellationToken ct)
    {
        if (actor.Id == Guid.Empty) throw new UnauthorizedAccessException("Authentication is required.");
        var entry = await store.FindEnrollmentAsync(enrollmentId, ct) ?? throw new ResourceNotFoundException("Enrollment not found.");
        if (entry.StudentId != actor.Id) throw new UnauthorizedAccessException("You can only access your own enrollment.");
        return entry;
    }
    public async Task CanReadExamAsync(Guid examId, CancellationToken ct)
    {
        if (actor.IsAdministrator || actor.IsLecturer) { await CanManageExamAsync(examId, ct); return; }
        if (actor.Id == Guid.Empty || !await store.IsEnrolledAsync(examId, actor.Id, ct))
            throw new UnauthorizedAccessException("You are not enrolled in this exam.");
    }
}
