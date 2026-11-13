using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IExamNotifier
{
    Task NotifyChangedAsync(Guid examId, CancellationToken cancellationToken);
}
