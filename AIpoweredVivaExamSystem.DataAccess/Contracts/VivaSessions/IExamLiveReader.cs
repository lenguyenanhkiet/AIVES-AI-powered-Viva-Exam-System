using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;
namespace AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;

public interface IExamLiveReader
{
    Task<ExamSummary> SummaryAsync(Guid examId, CancellationToken ct);
    Task<ExamLiveStatus> ReadAsync(Guid examId, CancellationToken ct);
}
