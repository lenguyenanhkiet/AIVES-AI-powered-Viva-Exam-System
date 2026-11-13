namespace AIpoweredVivaExamSystem.Business.Grading;

public interface IStudentResultAuthorization
{
    Task<bool> CanReadAsync(Guid studentId, Guid sessionId, CancellationToken ct);
}
