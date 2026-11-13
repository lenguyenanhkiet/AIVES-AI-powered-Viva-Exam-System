namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public sealed class MyVivaSessionsService(IMyVivaSessionsReader reader)
{
    public Task<IReadOnlyList<MyVivaExam>> ListAsync(CancellationToken ct) => reader.ListAsync(ct);
}
