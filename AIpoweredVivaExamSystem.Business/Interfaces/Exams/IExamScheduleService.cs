using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IExamScheduleService
{
    Task<SchedulePreview> PreviewAsync(Guid examId, CancellationToken ct);
    Task ApplyAsync(Guid examId, string previewToken, CancellationToken ct);
    Task<IReadOnlyList<ScheduleSlot>> MyScheduleAsync(CancellationToken ct);
}
