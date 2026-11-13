using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public sealed class VivaSessionService(IVivaSessionStore store, ExamAccessGuard guard, ICurrentUser actor) : IVivaSessionService
{
    public async Task<StudentSession> GetOrCreateMySessionAsync(Guid enrollmentId, CancellationToken ct)
    {
        RequireStudent();
        await guard.OwnExamStudentAsync(enrollmentId, ct);
        return await store.GetOrCreateAsync(enrollmentId, ct);
    }

    public async Task CompleteMySessionAsync(Guid sessionId, CancellationToken ct)
    {
        RequireStudent();
        await guard.OwnExamStudentAsync(await store.FindEnrollmentAsync(sessionId, ct), ct);
        await store.CompleteAsync(sessionId, ct);
    }

    private void RequireStudent()
    {
        if (actor.Id == Guid.Empty || actor.IsAdministrator || actor.IsLecturer)
            throw new UnauthorizedAccessException("Student access is required.");
    }
}
