namespace AIpoweredVivaExamSystem.Application.Exams;

public sealed class ExamFoundationService(ExamAccessGuard guard, IExamFoundationReader reader)
{
    public Task<IReadOnlyList<ExamSummary>> ListAsync(CancellationToken ct)
    { guard.RequireManager(); return reader.ListManagedAsync(ct); }
    public async Task<ExamFoundationCounts> CountsAsync(Guid examId, CancellationToken ct)
    { await guard.CanManageExamAsync(examId, ct); return await reader.CountsAsync(examId, ct); }
}
