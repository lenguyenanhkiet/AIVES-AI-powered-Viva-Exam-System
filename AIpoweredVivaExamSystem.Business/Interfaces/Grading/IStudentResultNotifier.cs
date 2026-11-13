namespace AIpoweredVivaExamSystem.Business.Grading;

public interface IStudentResultNotifier
{
    Task NotifyChangedAsync(Guid sessionId, CancellationToken ct);
}
