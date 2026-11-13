namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Grading;

public interface IStudentResultAccess
{
    Task<bool> CanReadAsync(Guid studentId, Guid sessionId, CancellationToken ct);
}
