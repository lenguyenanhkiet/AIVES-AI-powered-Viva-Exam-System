using AIpoweredVivaExamSystem.Application.Exams;

namespace AIpoweredVivaExamSystem.Web.Realtime;

// Explicit temporary adapter. Dung's module replaces this registration with SignalRExamNotifier.
public sealed class PendingExamNotifier(ILogger<PendingExamNotifier> logger) : IExamNotifier
{
    public Task NotifyChangedAsync(Guid examId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Exam {ExamId} changed; SignalR adapter is pending integration", examId);
        return Task.CompletedTask;
    }
}
