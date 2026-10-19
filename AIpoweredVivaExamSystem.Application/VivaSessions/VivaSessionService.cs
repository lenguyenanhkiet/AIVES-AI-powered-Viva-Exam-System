using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public sealed record MyVivaExam(Guid EnrollmentId, string Name, string Status, DateTimeOffset? ScheduledAt);
public interface IMyVivaSessionsReader
{
    Task<IReadOnlyList<MyVivaExam>> ListAsync(CancellationToken ct);
}

public interface IVivaSessionStore
{
    Task<StudentSession> GetOrCreateAsync(Guid enrollmentId, CancellationToken ct);
    Task<Guid> FindEnrollmentAsync(Guid sessionId, CancellationToken ct);
    Task CompleteAsync(Guid sessionId, CancellationToken ct);
}

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
