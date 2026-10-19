using AIpoweredVivaExamSystem.Application.Exams;
namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public sealed record LiveStudent(Guid EnrollmentId, string Name, DateTimeOffset? ScheduledAt, string SessionStatus);
public sealed record ExamLiveStatus(string Status, ExamFoundationCounts Counts, int CompletedSessions, IReadOnlyList<LiveStudent> Roster);
public interface IExamLiveReader
{
    Task<ExamSummary> SummaryAsync(Guid examId, CancellationToken ct);
    Task<ExamLiveStatus> ReadAsync(Guid examId, CancellationToken ct);
}
