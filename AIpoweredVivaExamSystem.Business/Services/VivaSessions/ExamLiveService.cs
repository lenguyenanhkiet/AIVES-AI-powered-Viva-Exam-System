namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public sealed class ExamLiveService(IExamLiveReader reader, ExamAccessGuard guard)
{
    public async Task<ExamSummary> SummaryAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        return await reader.SummaryAsync(examId, ct);
    }

    public async Task<ExamLiveStatus> ReadAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        return await reader.ReadAsync(examId, ct);
    }
}
