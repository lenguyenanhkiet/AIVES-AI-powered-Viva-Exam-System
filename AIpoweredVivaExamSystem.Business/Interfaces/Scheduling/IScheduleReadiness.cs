using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Scheduling;

public interface IScheduleReadiness
{
    Task<bool> IsFullyScheduledAsync(Guid examId, CancellationToken ct);
}
