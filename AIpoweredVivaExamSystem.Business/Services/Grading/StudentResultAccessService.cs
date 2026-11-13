namespace AIpoweredVivaExamSystem.Business.Grading;

public sealed class StudentResultAccessService(IStudentResultAccess access) : IStudentResultAuthorization
{
    public Task<bool> CanReadAsync(Guid studentId, Guid sessionId, CancellationToken ct) =>
        access.CanReadAsync(studentId, sessionId, ct);
}
