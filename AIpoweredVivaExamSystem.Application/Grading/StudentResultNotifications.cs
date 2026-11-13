namespace AIpoweredVivaExamSystem.Application.Grading;

public interface IStudentResultAccess
{
    Task<bool> CanReadAsync(Guid studentId, Guid sessionId, CancellationToken ct);
}

public interface IStudentResultNotifier
{
    Task NotifyChangedAsync(Guid sessionId, CancellationToken ct);
}
